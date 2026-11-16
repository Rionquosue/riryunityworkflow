using System.Collections.Generic;
using BlackStartX.GestureManager;
using BlackStartX.GestureManager.Editor.Modules.Vrc3;
using BlackStartX.GestureManager.Editor.Modules.Vrc3.Params;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator's output to Gesture Manager: avatar parameters are read and written through the active module's own
    /// parameter registry (ModuleVrc3.Params and Vrc3Param.Set, both public), so a value reaches every playable layer
    /// that has the parameter, as Gesture Manager's own menu does it. Nothing of Gesture Manager is reached by
    /// reflection. It is bound to one Gesture Manager object and the module it runs: when the avatar or its graph is
    /// replaced the module is a new object, Valid turns false, and FTEmulator binds again.
    /// Gesture Manager does not write a parameter an animation curve controls; FTEmulator sees that when it reads the
    /// value back. Gesture Manager's own OSC, when its listener is on, uses the same default port as FTEmulator's
    /// receiver: only one of the two can listen there. Built against Gesture Manager 3.9.9.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class FTEmulatorGestureManager : IFTEmulatorOutput
    {
        static FTEmulatorGestureManager()
        {
            FTEmulator.CreateOutput = () => new FTEmulatorGestureManager();
            FTEmulator.FindManagers = () => Object.FindObjectsOfType<GestureManager>();
            FTEmulator.ManagerType = typeof(GestureManager);
        }

        private GestureManager manager;
        private ModuleVrc3 module;
        private List<FTEmulatorAvatarParameter> parameters;
        private int listed = -1;

        public string Name
        {
            get { return GestureManager.Version; }
        }

        public bool Bind(Object target, out string problem)
        {
            problem = null;
            manager = target as GestureManager;
            if (manager == null && target is GameObject holder) manager = holder.GetComponent<GestureManager>();
            module = manager != null ? manager.Module as ModuleVrc3 : null;
            parameters = null;
            listed = -1;
            if (manager == null) problem = "Select a Gesture Manager object.";
            else if (module == null || module.IsInvalid()) problem = "That Gesture Manager runs no avatar. Select the avatar in Gesture Manager first.";
            if (problem == null) return true;
            module = null;
            return false;
        }

        public bool Valid
        {
            get { return manager != null && module != null && ReferenceEquals(manager.Module, module) && !module.IsInvalid(); }
        }

        public GameObject Avatar
        {
            get { return module != null ? module.Avatar : null; }
        }

        public IReadOnlyList<FTEmulatorAvatarParameter> Parameters
        {
            get
            {
                if (module == null) return new List<FTEmulatorAvatarParameter>();
                // Listed again when Gesture Manager's registry grew or shrank.
                if (parameters != null && listed == module.Params.Count) return parameters;
                parameters = new List<FTEmulatorAvatarParameter>(module.Params.Count);
                foreach (KeyValuePair<string, Vrc3Param> pair in module.Params)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null) continue;
                    parameters.Add(new FTEmulatorAvatarParameter
                    {
                        Name = pair.Key,
                        Type = pair.Value.Type == AnimatorControllerParameterType.Float ? FTEmulatorValueType.Float
                            : pair.Value.Type == AnimatorControllerParameterType.Int ? FTEmulatorValueType.Int : FTEmulatorValueType.Bool,
                    });
                }
                listed = module.Params.Count;
                return parameters;
            }
        }

        public bool Write(string name, float value)
        {
            Vrc3Param parameter = module != null ? module.GetParam(name) : null;
            if (parameter == null) return false;
            parameter.Set(module, value);
            return true;
        }

        public bool TryRead(string name, out float value)
        {
            Vrc3Param parameter = module != null ? module.GetParam(name) : null;
            value = parameter != null ? parameter.FloatValue() : 0f;
            return parameter != null;
        }

        public void Release()
        {
            module = null;
            manager = null;
            parameters = null;
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator Gesture Manager output (compiled only with Gesture Manager 3.9+, RIRYTOOLS_GESTURE_MANAGER)
// ============================================================================
//
// FTEmulatorGestureManager (this file): IFTEmulatorOutput (Editor/VRChat/FTEmulator/FTEmulatorTypes.cs)
//   static constructor       [InitializeOnLoad]: sets FTEmulator.CreateOutput, FindManagers and ManagerType.
//   Bind(target, out problem)  A GestureManager component, or the object that has one; its Module must be a valid ModuleVrc3
//                            (an avatar is running).
//   Valid                    The manager still runs the module that was bound.
//   Avatar                   ModuleBase.Avatar.
//   Parameters               ModuleVrc3.Params as name and type (a Trigger is a Bool), listed again when the count changes.
//   Write(name, value)       Vrc3Param.Set(module, value): every playable layer that has the parameter. False when Gesture
//                            Manager has no such parameter.
//   TryRead(name, out value) Vrc3Param.FloatValue().
//   Release()                Forgets the manager and the module.
