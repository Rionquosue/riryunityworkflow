using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// A tool window that hears Undo and Redo: OnUndoRedo is called after each, from when the window is enabled until
    /// it is disabled. A window with its own OnEnable or OnDisable overrides it and calls the base one.
    /// </summary>
    internal abstract class RiryToolUndoWindow : EditorWindow
    {
        protected virtual void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        /// <summary>After an Undo or Redo.</summary>
        protected abstract void OnUndoRedo();
    }
}

// ============================================================================
// GLOSSARY: Undo-aware tool window
// ============================================================================
//
// RiryToolUndoWindow (this file)
//   OnEnable / OnDisable     Subscribe and unsubscribe OnUndoRedo to Undo.undoRedoPerformed.
//   OnUndoRedo()             Implemented by each window: what to refresh after an Undo or Redo.
