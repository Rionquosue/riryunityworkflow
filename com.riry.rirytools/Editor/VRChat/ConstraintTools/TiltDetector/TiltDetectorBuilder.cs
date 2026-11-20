using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Constraint Tool's axis detectors: VRC contacts that read how far an object (Source) has moved left/right and up/down
    /// across a flat plane fixed to another object (Relative to). A new object under Relative to, TiltDetector_&lt;source&gt;,
    /// sits at Source's rest position, its axes those of Relative to (Local) or the world (World), and holds the Proximity
    /// receivers laid out in the plane: Dual axis two in an L (Right, Up), Three axis three in a triangle 120° apart (Top,
    /// Bottom Left, Bottom Right). A new object under Source, TiltSender_&lt;source&gt;, holds the sender. Each receiver is
    /// Offset from the rest position with a radius of twice that, so it reads 0.5 at rest. Nothing existing is changed: on
    /// a prefab instance both are added objects (overrides), so the prefab stays packed. One undo step.
    /// </summary>
    internal static class TiltDetectorBuilder
    {
        /// <summary>The axis the flat plane faces (its normal); the receivers lie in the other two.</summary>
        internal enum Plane { X, Y, Z }

        /// <summary>Whose axes the plane is taken from: Relative to's own, or the world's.</summary>
        internal enum Space { Local, World }

        internal const string UndoName = "Axis detector";
        internal const string RootPrefix = "TiltDetector_";
        internal const string SenderPrefix = "TiltSender_";
        internal const string ParameterPrefix = "Tilt/";
        internal const float DefaultOffset = 0.05f;
        internal const float SenderRadius = 0.001f;
        // Each receiver's radius as a multiple of Offset: twice, so it reads 0.5 at rest.
        internal const float RadiusScale = 2f;
        private const float Cos30 = 0.8660254f;

        /// <summary>Receiver names (also the parameter names after ParameterPrefix) and their directions in the plane (right, up).</summary>
        internal static readonly (string Name, Vector2 Direction)[] Dual = { ("Right", new Vector2(1f, 0f)), ("Up", new Vector2(0f, 1f)) };
        internal static readonly (string Name, Vector2 Direction)[] Three =
            { ("Top", new Vector2(0f, 1f)), ("BottomLeft", new Vector2(-Cos30, -0.5f)), ("BottomRight", new Vector2(Cos30, -0.5f)) };

        internal sealed class Settings
        {
            internal Transform Source;
            internal Transform RelativeTo;
            internal Plane Plane = Plane.Y;
            internal Space Space = Space.Local;
            internal bool ThreeAxis;
            internal float Offset = DefaultOffset;
        }

        internal sealed class Result
        {
            internal GameObject Receivers;
            internal GameObject Sender;
        }

        /// <summary>Why the settings can't be built, or null.</summary>
        internal static string Problem(Settings settings)
        {
            if (settings.Source == null) return "Assign Source.";
            if (settings.RelativeTo == null) return "Assign Relative to.";
            if (settings.Source == settings.RelativeTo) return "Source and Relative to must be different objects.";
            if (EditorUtility.IsPersistent(settings.Source) || EditorUtility.IsPersistent(settings.RelativeTo))
                return "Use objects in a scene or in Prefab Mode, not a prefab asset.";
            if (settings.Source.gameObject.scene != settings.RelativeTo.gameObject.scene) return "Source and Relative to must be in the same scene.";
            if (settings.RelativeTo.IsChildOf(settings.Source)) return "Relative to can't be inside Source: it would move with it.";
            if (!(settings.Offset > 0f) || float.IsInfinity(settings.Offset)) return "Offset must be above 0.";
            return null;
        }

        /// <summary>The plane's right and up directions, in Relative to's local space (Local) or in world space (World).</summary>
        internal static void Axes(Plane plane, out Vector3 right, out Vector3 up)
        {
            switch (plane)
            {
                case Plane.X: right = Vector3.forward; up = Vector3.up; break;
                case Plane.Y: right = Vector3.right; up = Vector3.forward; break;
                default: right = Vector3.right; up = Vector3.up; break;
            }
        }

        /// <summary>Creates the receivers and the sender as one undo step. Throws when Problem is not null.</summary>
        internal static Result Build(Settings settings)
        {
            string problem = Problem(settings);
            if (problem != null) throw new InvalidOperationException(problem);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            // One tag per detector, so two detectors never read each other's sender.
            string tag = "RiryTilt_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string name = settings.Source.name;

            var result = new Result();
            Transform root = Create(RootPrefix + name, settings.RelativeTo);
            root.position = settings.Source.position;
            if (settings.Space == Space.World) root.rotation = Quaternion.identity;
            else root.localRotation = Quaternion.identity;
            result.Receivers = root.gameObject;

            Axes(settings.Plane, out Vector3 right, out Vector3 up);
            foreach ((string Name, Vector2 Direction) receiver in settings.ThreeAxis ? Three : Dual)
            {
                Transform holder = Create(receiver.Name, root);
                holder.localPosition = (right * receiver.Direction.x + up * receiver.Direction.y) * settings.Offset;
                holder.localRotation = Quaternion.identity;
                var contact = Undo.AddComponent<VRCContactReceiver>(holder.gameObject);
                contact.shapeType = ContactBase.ShapeType.Sphere;
                contact.radius = settings.Offset * RadiusScale;
                contact.collisionTags = new List<string> { tag };
                contact.allowSelf = true;
                contact.allowOthers = false;
                contact.receiverType = ContactReceiver.ReceiverType.Proximity;
                contact.parameter = ParameterPrefix + receiver.Name;
                Dirty(contact);
            }

            Transform sender = Create(SenderPrefix + name, settings.Source);
            sender.localPosition = Vector3.zero;
            sender.localRotation = Quaternion.identity;
            var send = Undo.AddComponent<VRCContactSender>(sender.gameObject);
            send.shapeType = ContactBase.ShapeType.Sphere;
            send.radius = SenderRadius;
            send.collisionTags = new List<string> { tag };
            Dirty(send);
            result.Sender = sender.gameObject;

            Undo.CollapseUndoOperations(group);
            return result;
        }

        private static Transform Create(string name, Transform parent)
        {
            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            Undo.SetTransformParent(created.transform, parent, UndoName);
            created.transform.localScale = Vector3.one;
            created.layer = parent.gameObject.layer;
            return created.transform;
        }

        private static void Dirty(Component component)
        {
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }
}

// ============================================================================
// GLOSSARY: Axis detector builder
// ============================================================================
//
// TiltDetectorBuilder (this file)
//   Plane                    X / Y / Z: the axis the flat plane faces; Space: Local (Relative to's axes) / World.
//   Dual / Three             Receiver names and in-plane directions: Right, Up (L) / Top, BottomLeft, BottomRight (120°).
//   Settings                 Source, RelativeTo, Plane, Space, ThreeAxis, Offset.
//   Problem(settings)        Why it can't be built, or null.
//   Axes(plane, out right, out up)  The plane's right and up directions.
//   Build(settings)          TiltDetector_<source> under Relative to with the receivers, TiltSender_<source> under Source
//                            with the sender, one shared tag; one undo step.
