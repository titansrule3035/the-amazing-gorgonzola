using Godot;
using System;
using System.Collections.Generic;

// ======================================================================
// History
// ======================================================================

/// <summary>
/// Undo/redo stacks. Everything the editor changes goes through Execute() so it can be undone.
/// Use BeginGroup()/EndGroup() to merge several commands (e.g. one mouse drag) into a single undo step.
/// </summary>
public sealed class EditorHistory
{
    readonly List<EditorCommand> undos = new();   // can be private now
    readonly List<EditorCommand> redos = new();
    readonly int maxSteps;

    int groupDepth;
    List<EditorCommand>? _group;

    // --- dirty tracking ---
    EditorCommand? savedCommand;   // top of undo stack at last save (null = empty stack)
    bool cannotAccess;                // saved state can no longer be reached via undo/redo
    bool externallyDirty;            // changes made outside the history
    bool wasDirty;

    public event Action? Changed;
    public event Action<bool>? DirtyChanged;

    public EditorHistory(int maxSteps) => maxSteps = Math.Max(1, maxSteps);

    public bool GroupOpen => groupDepth > 0;
    public bool CanUndo => undos.Count > 0 && !GroupOpen;
    public bool CanRedo => redos.Count > 0 && !GroupOpen;

    EditorCommand? Top => undos.Count > 0 ? undos[^1] : null;
    bool PendingGroupChanges => _group is { Count: > 0 };

    public bool IsDirty => externallyDirty || cannotAccess || PendingGroupChanges || !ReferenceEquals(Top, savedCommand);

    /// <summary>Call after the file was written successfully.</summary>
    public void MarkSaved()
    {
        savedCommand = Top;
        cannotAccess = false;
        externallyDirty = false;
        NotifyChanged();
    }

    /// <summary>For changes that bypass the history (level name, settings, etc.).</summary>
    public void MarkDirty()
    {
        externallyDirty = true;
        NotifyChanged();
    }

    public void Execute(EditorCommand command)
    {
        if (command.IsNoOp) return;

        command.Do();
        Record(command);
        NotifyChanged();
    }

    public void BeginGroup()
    {
        if (groupDepth++ == 0) _group = new List<EditorCommand>();
    }

    public void EndGroup()
    {
        if (groupDepth == 0) return;
        if (--groupDepth > 0) return;

        var commands = _group;
        _group = null;

        if (commands == null || commands.Count == 0)
        {
            NotifyChanged();
            return;
        }

        Push(commands.Count == 1 ? commands[0] : new CompositeCommand(commands));
        NotifyChanged();
    }

    public bool Undo()
    {
        if (!CanUndo) return false;

        var command = undos[^1];
        undos.RemoveAt(undos.Count - 1);
        command.Undo();
        redos.Add(command);

        NotifyChanged();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;

        var command = redos[^1];
        redos.RemoveAt(redos.Count - 1);
        command.Do();
        undos.Add(command);

        NotifyChanged();
        return true;
    }

    /// <summary>Call when a level is loaded or created. The new level starts clean.</summary>
    public void Clear()
    {
        foreach (var c in undos) c.Dispose();
        foreach (var c in redos) c.Dispose();

        undos.Clear();
        redos.Clear();
        _group = null;
        groupDepth = 0;

        savedCommand = null;
        cannotAccess = false;
        externallyDirty = false;

        NotifyChanged();
    }

    void Record(EditorCommand command)
    {
        if (GroupOpen) _group!.Add(command);
        else Push(command);
    }

    void Push(EditorCommand command)
    {
        // The saved state lives in the redo branch we're about to destroy.
        if (savedCommand != null && redos.Contains(savedCommand))
        {
            savedCommand = null;
            cannotAccess = true;
        }

        foreach (var c in redos) c.Dispose();
        redos.Clear();

        undos.Add(command);

        while (undos.Count > maxSteps)
        {
            var oldest = undos[0];

            // Saved at the very bottom (null), or the saved command itself is being dropped:
            // undoing back to an empty stack no longer reproduces the saved file.
            if (savedCommand == null || ReferenceEquals(oldest, savedCommand))
            {
                savedCommand = null;
                cannotAccess = true;
            }

            oldest.Dispose();
            undos.RemoveAt(0);
        }
    }

    void NotifyChanged()
    {
        Changed?.Invoke();

        bool now = IsDirty;
        if (now == wasDirty) return;

        wasDirty = now;
        DirtyChanged?.Invoke(now);
    }
}

// ======================================================================
// Command base
// ======================================================================

public abstract class EditorCommand
{
    /// <summary>Called after Do (false) or Undo (true). Used to keep the selection in sync.</summary>
    public Action<bool>? OnApplied;

    /// <summary>True if running this command would change nothing. History ignores such commands.</summary>
    public virtual bool IsNoOp => false;

    public void Do()
    {
        DoCore();
        OnApplied?.Invoke(false);
    }

    public void Undo()
    {
        UndoCore();
        OnApplied?.Invoke(true);
    }

    protected abstract void DoCore();
    protected abstract void UndoCore();

    /// <summary>The command is gone for good. Free anything it still owns (e.g. detached nodes).</summary>
    public virtual void Dispose() { }
}

// ======================================================================
// Tiles
// ======================================================================

public readonly record struct TileSnapshot(int SourceId, Vector2I Atlas, int Alternative)
{
    public static readonly TileSnapshot Empty = new(-1, Vector2I.Zero, 0);

    public static TileSnapshot Capture(TileMapLayer map, Vector2I cell)
    {
        int id = map.GetCellSourceId(cell);

        return id == -1
            ? Empty
            : new TileSnapshot(id, map.GetCellAtlasCoords(cell), map.GetCellAlternativeTile(cell));
    }

    public void ApplyTo(TileMapLayer map, Vector2I cell)
    {
        if (SourceId == -1)
        {
            map.EraseCell(cell);
        }
        else
        {
            map.SetCell(cell, SourceId, Atlas, Alternative);
        }
    }
}

/// <summary>Any change to a set of tile cells: place, erase, move, delete.</summary>
public sealed class TileChangeCommand : EditorCommand
{
    readonly TileMapLayer _map;
    readonly Dictionary<Vector2I, (TileSnapshot Before, TileSnapshot After)> _cells = new();

    public TileChangeCommand(TileMapLayer map) => _map = map;

    public bool IsEmpty => _cells.Count == 0;

    public override bool IsNoOp => _cells.Count == 0;

    public void Set(Vector2I cell, TileSnapshot before, TileSnapshot after)
    {
        if (before == after)
        {
            return;
        }

        _cells[cell] = (before, after);
    }

    protected override void DoCore()
    {
        foreach (var (cell, state) in _cells)
        {
            state.After.ApplyTo(_map, cell);
        }
    }

    protected override void UndoCore()
    {
        foreach (var (cell, state) in _cells)
        {
            state.Before.ApplyTo(_map, cell);
        }
    }
}

// ======================================================================
// Objects
// ======================================================================

/// <summary>
/// Adds nodes to, or removes them from, the level. Removed nodes are detached rather than freed,
/// so undo can put the exact same instance back. They're freed when the command is discarded.
/// </summary>
public sealed class ObjectAttachCommand : EditorCommand
{
    sealed class Entry
    {
        public Node2D Node;
        public Node Parent;
        public Vector2 GlobalPos;
        public int Index = -1;
    }

    readonly List<Entry> _entries = new();
    readonly bool _adds;

    ObjectAttachCommand(bool adds) => _adds = adds;

    public override bool IsNoOp => _entries.Count == 0;

    public static ObjectAttachCommand Add(Node parent, Node2D node, Vector2 globalPos)
    {
        var cmd = new ObjectAttachCommand(true);
        cmd._entries.Add(new Entry { Node = node, Parent = parent, GlobalPos = globalPos });
        return cmd;
    }

    public static ObjectAttachCommand Remove(IEnumerable<Node2D> nodes)
    {
        var cmd = new ObjectAttachCommand(false);

        foreach (var node in nodes)
        {
            var parent = node.GetParent();
            if (parent == null) continue;

            cmd._entries.Add(new Entry
            {
                Node = node,
                Parent = parent,
                GlobalPos = node.GlobalPosition,
                Index = node.GetIndex()
            });
        }

        return cmd;
    }

    protected override void DoCore()
    {
        if (_adds)
        {
            Attach();
        }
        else
        {
            Detach();
        }
    }

    protected override void UndoCore()
    {
        if (_adds)
        {
            Detach();
        }
        else
        {
            Attach();
        }
    }

    void Attach()
    {
        foreach (var e in _entries)
        {
            if (!GodotObject.IsInstanceValid(e.Node) || !GodotObject.IsInstanceValid(e.Parent))
            {
                continue;
            }

            if (e.Node.GetParent() != null)
            {
                continue;
            }

            e.Parent.AddChild(e.Node);

            if (e.Index >= 0 && e.Index < e.Parent.GetChildCount())
            {
                e.Parent.MoveChild(e.Node, e.Index);
            }

            // Set after AddChild so the parent's own transform is taken into account.
            e.Node.GlobalPosition = e.GlobalPos;
        }
    }

    void Detach()
    {
        foreach (var e in _entries)
        {
            if (!GodotObject.IsInstanceValid(e.Node) || e.Node.GetParent() != e.Parent)
            {
                continue;
            }

            e.Index = e.Node.GetIndex();
            e.GlobalPos = e.Node.GlobalPosition;
            e.Parent.RemoveChild(e.Node);
        }
    }

    public override void Dispose()
    {
        foreach (var e in _entries)
        {
            if (GodotObject.IsInstanceValid(e.Node) && e.Node.GetParent() == null)
            {
                e.Node.QueueFree();
            }
        }
    }
}

public sealed class ObjectMoveCommand : EditorCommand
{
    public readonly record struct Entry(Node2D Node, Vector2 FromPos, Vector2 ToPos, Vector2I FromCell, Vector2I ToCell);

    readonly List<Entry> _entries;

    public ObjectMoveCommand(List<Entry> entries) => _entries = entries;

    public override bool IsNoOp => _entries.Count == 0;

    protected override void DoCore()
    {
        foreach (var e in _entries)
        {
            Apply(e.Node, e.ToPos, e.ToCell);
        }
    }

    protected override void UndoCore()
    {
        foreach (var e in _entries)
        {
            Apply(e.Node, e.FromPos, e.FromCell);
        }
    }

    static void Apply(Node2D node, Vector2 pos, Vector2I cell)
    {
        if (!GodotObject.IsInstanceValid(node))
        {
            return;
        }

        node.GlobalPosition = pos;
        node.SetMeta(EditorObject.MetaCell, cell);
    }
}

// ======================================================================
// Utility commands
// ======================================================================

public sealed class CompositeCommand : EditorCommand
{
    readonly List<EditorCommand> _commands;

    public CompositeCommand(List<EditorCommand> commands) => _commands = commands;

    public override bool IsNoOp => _commands.TrueForAll(c => c.IsNoOp);

    protected override void DoCore()
    {
        foreach (var c in _commands)
        {
            c.Do();
        }
    }

    protected override void UndoCore()
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }

    public override void Dispose()
    {
        foreach (var c in _commands)
        {
            c.Dispose();
        }
    }
}

/// <summary>Undoable wrapper around two delegates. Handy for item-specific context menu options.</summary>
public sealed class ActionCommand : EditorCommand
{
    readonly Action _do;
    readonly Action undos;

    public ActionCommand(Action doIt, Action undoIt)
    {
        _do = doIt;
        undos = undoIt;
    }

    protected override void DoCore() => _do();
    protected override void UndoCore() => undos();
}