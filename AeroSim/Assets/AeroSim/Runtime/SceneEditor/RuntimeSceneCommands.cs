using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.Runtime.SceneEditor
{
    internal interface IRuntimeSceneCommand
    {
        string Label { get; }
        void Execute();
        void Undo();
        void Dispose();
    }

    internal sealed class RuntimeCommandHistory
    {
        private readonly Stack<IRuntimeSceneCommand> undo = new();
        private readonly Stack<IRuntimeSceneCommand> redo = new();

        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;
        public string UndoLabel => undo.Count == 0 ? string.Empty : undo.Peek().Label;
        public string RedoLabel => redo.Count == 0 ? string.Empty : redo.Peek().Label;
        public event Action Changed;

        public void Execute(IRuntimeSceneCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            command.Execute();
            undo.Push(command);
            DisposeStack(redo);
            Changed?.Invoke();
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            var command = undo.Pop();
            command.Undo();
            redo.Push(command);
            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            var command = redo.Pop();
            command.Execute();
            undo.Push(command);
            Changed?.Invoke();
            return true;
        }

        public void RollbackAll()
        {
            while (Undo()) { }
        }

        public void Clear()
        {
            DisposeStack(undo);
            DisposeStack(redo);
            Changed?.Invoke();
        }

        private static void DisposeStack(Stack<IRuntimeSceneCommand> stack)
        {
            while (stack.Count > 0) stack.Pop().Dispose();
        }
    }

    internal sealed class SpawnDraftCommand : IRuntimeSceneCommand
    {
        private readonly RuntimeSceneObject item;
        private readonly Action<RuntimeSceneObject, bool> setActive;
        public string Label => "Spawn " + item.PrefabId;

        public SpawnDraftCommand(RuntimeSceneObject item, Action<RuntimeSceneObject, bool> setActive)
        {
            this.item = item;
            this.setActive = setActive;
        }

        public void Execute() => setActive(item, true);
        public void Undo() => setActive(item, false);
        public void Dispose()
        {
            if (item == null || item.gameObject.activeSelf) return;
            item.GetComponent<DraftMaterialLifetime>()?.Release();
            if (Application.isPlaying) UnityEngine.Object.Destroy(item.gameObject);
            else UnityEngine.Object.DestroyImmediate(item.gameObject);
        }
    }

    internal sealed class SetDraftActiveCommand : IRuntimeSceneCommand
    {
        private readonly RuntimeSceneObject item;
        private readonly bool next;
        private readonly Action<RuntimeSceneObject, bool> setActive;
        public string Label { get; }

        public SetDraftActiveCommand(RuntimeSceneObject item, bool next, string label, Action<RuntimeSceneObject, bool> setActive)
        {
            this.item = item;
            this.next = next;
            this.setActive = setActive;
            Label = label;
        }

        public void Execute() => setActive(item, next);
        public void Undo() => setActive(item, !next);
        public void Dispose() { }
    }

    internal sealed class TransformDraftCommand : IRuntimeSceneCommand
    {
        private readonly RuntimeSceneObject item;
        private readonly RuntimeTransformState before;
        private readonly RuntimeTransformState after;
        private readonly Action<RuntimeSceneObject> changed;
        public string Label => "Transform " + item.name;

        public TransformDraftCommand(RuntimeSceneObject item, RuntimeTransformState before, RuntimeTransformState after, Action<RuntimeSceneObject> changed)
        {
            this.item = item;
            this.before = before;
            this.after = after;
            this.changed = changed;
        }

        public void Execute()
        {
            if (item == null) return;
            after.Apply(item.transform);
            changed(item);
        }

        public void Undo()
        {
            if (item == null) return;
            before.Apply(item.transform);
            changed(item);
        }

        public void Dispose() { }
    }

    internal sealed class CompositeDraftCommand : IRuntimeSceneCommand
    {
        private readonly List<IRuntimeSceneCommand> commands;
        public string Label { get; }

        public CompositeDraftCommand(string label, List<IRuntimeSceneCommand> commands)
        {
            Label = label;
            this.commands = commands;
        }

        public void Execute()
        {
            for (int i = 0; i < commands.Count; i++) commands[i].Execute();
        }

        public void Undo()
        {
            for (int i = commands.Count - 1; i >= 0; i--) commands[i].Undo();
        }

        public void Dispose()
        {
            foreach (var command in commands) command.Dispose();
        }
    }
}
