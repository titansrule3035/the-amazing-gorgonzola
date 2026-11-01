using Godot;
using System;
using System.Collections.Generic;

// ======================================================================
// History
// ======================================================================

/// <summary>
/// Undo/redo stacks. Everything the editor changes goes through <see cref="Execute"/> so it can be undone.
/// Use <see cref="BeginGroup"/> / <see cref="EndGroup"/> to merge several commands (e.g. one mouse drag)
/// into a single undo step. Groups nest; only the outermost EndGroup commits.
/// </summary>
public sealed class EditorHistory
{
    List<EditorCommand> undos = new();
    List<EditorCommand> redos = new();
    int maxSteps;
    int groupDepth;
    List<EditorCommand>? group;

    // --- dirty tracking ---
    // Top of the undo stack at the last save (null = empty stack).
    EditorCommand? savedCommand;
    // The saved state can no longer be reached through undo/redo.
    bool cannotAccess;
    // Changes made outside the history (level name, settings, etc).
    bool externallyDirty;
    bool wasDirty;

    /// <summary>Raised after every change to the stacks, a group, or the saved marker.</summary>
    public event Action? Changed;

    /// <summary>Raised only when <see cref="IsDirty"/> flips. The argument is the new value.</summary>
    public event Action<bool>? DirtyChanged;

    /// <param name="maxSteps">How many undo steps to keep. Clamped to at least 1.</param>
    public EditorHistory(int maxSteps)
    {
        this.maxSteps = Math.Max(1, maxSteps);
    }

    /// <summary>True between BeginGroup and the matching EndGroup.</summary>
    public bool groupOpen => groupDepth > 0;

    /// <summary>Undo is blocked while a group is open, so a stroke can't be undone halfway through.</summary>
    public bool canUndo => undos.Count > 0 && !groupOpen;

    public bool canRedo => redos.Count > 0 && !groupOpen;

    EditorCommand? topCommand => undos.Count > 0 ? undos[^1] : null;

    bool groupChangesPending => group is { Count: > 0 };

    /// <summary>True when the level differs from what was last written to disk.</summary>
    public bool IsDirty => externallyDirty || cannotAccess || groupChangesPending || !ReferenceEquals(topCommand, savedCommand);

    /// <summary>Call after the file was written successfully.</summary>
    public void MarkSaved()
    {
        savedCommand = topCommand;
        cannotAccess = false;
        externallyDirty = false;
        NotifyChanged();
    }

    /// <summary>For changes that bypass the history (level name, settings, etc).</summary>
    public void MarkDirty()
    {
        externallyDirty = true;
        NotifyChanged();
    }

    /// <summary>
    /// Runs the command and records it (into the open group, or as its own undo step).
    /// Commands that report <see cref="EditorCommand.IsNoOp"/> are ignored.
    /// </summary>
    public void Execute(EditorCommand command)
    {
        if (command.IsNoOp)
        {
            return;
        }
        command.Do();
        Record(command);
        NotifyChanged();
    }

    /// <summary>Starts collecting commands into one undo step. Safe to nest.</summary>
    public void BeginGroup()
    {
        if (groupDepth++ == 0)
        {
            group = new List<EditorCommand>();
        }
    }

    /// <summary>Ends the group. The outermost call pushes everything collected as a single undo step.</summary>
    public void EndGroup()
    {
        if (groupDepth == 0)
        {
            return;
        }
        if (--groupDepth > 0)
        {
            return;
        }
        List<EditorCommand>? commands = group;
        group = null;
        if (commands == null || commands.Count == 0)
        {
            NotifyChanged();
            return;
        }
        Push(commands.Count == 1 ? commands[0] : new CompositeCommand(commands));
        NotifyChanged();
    }

    /// <returns>True if something was undone.</returns>
    public bool Undo()
    {
        if (!canUndo)
        {
            return false;
        }
        EditorCommand command = undos[^1];
        undos.RemoveAt(undos.Count - 1);
        command.Undo();
        redos.Add(command);
        NotifyChanged();
        return true;
    }

    /// <returns>True if something was redone.</returns>
    public bool Redo()
    {
        if (!canRedo)
        {
            return false;
        }
        EditorCommand command = redos[^1];
        redos.RemoveAt(redos.Count - 1);
        command.Do();
        undos.Add(command);
        NotifyChanged();
        return true;
    }

    /// <summary>Call when a level is loaded or created. The new level starts clean.</summary>
    public void Clear()
    {
        foreach (EditorCommand command in undos)
        {
            command.Dispose();
        }
        foreach (EditorCommand command in redos)
        {
            command.Dispose();
        }
        // Commands of a group that is still open would otherwise leak (and any detached nodes never get freed).
        if (group != null)
        {
            foreach (EditorCommand command in group)
            {
                command.Dispose();
            }
        }
        undos.Clear();
        redos.Clear();
        group = null;
        groupDepth = 0;
        savedCommand = null;
        cannotAccess = false;
        externallyDirty = false;
        NotifyChanged();
    }

    void Record(EditorCommand command)
    {
        if (group != null)
        {
            group.Add(command);
            return;
        }
        Push(command);
    }

    void Push(EditorCommand command)
    {
        // The saved state lives in the redo branch we're about to destroy.
        if (savedCommand != null && redos.Contains(savedCommand))
        {
            savedCommand = null;
            cannotAccess = true;
        }
        foreach (EditorCommand redo in redos)
        {
            redo.Dispose();
        }
        redos.Clear();
        undos.Add(command);
        while (undos.Count > maxSteps)
        {
            EditorCommand oldest = undos[0];
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
        if (now == wasDirty)
        {
            return;
        }
        wasDirty = now;
        DirtyChanged?.Invoke(now);
    }
}

// ======================================================================
// Command base
// ======================================================================

/// <summary>
/// One undoable change. Subclasses implement DoCore / UndoCore; callers use Do / Undo,
/// which also raise <see cref="OnApplied"/>.
/// </summary>
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
    public virtual void Dispose()
    {
    }
}

// ======================================================================
// Tiles
// ======================================================================

/// <summary>The full state of one tile cell. <see cref="Empty"/> means no tile.</summary>
public record struct TileSnapshot(int SourceId, Vector2I Atlas, int Alternative)
{
    /// <summary>The state of a cell that holds no tile.</summary>
    public static TileSnapshot Empty => new(-1, Vector2I.Zero, 0);

    public static TileSnapshot Capture(TileMapLayer map, Vector2I cell)
    {
        int id = map.GetCellSourceId(cell);
        return id == -1 ? Empty : new TileSnapshot(id, map.GetCellAtlasCoords(cell), map.GetCellAlternativeTile(cell));
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
    TileMapLayer map;
    Dictionary<Vector2I, (TileSnapshot Before, TileSnapshot After)> cells = new();

    public TileChangeCommand(TileMapLayer map)
    {
        this.map = map;
    }

    public bool IsEmpty => cells.Count == 0;

    /// <summary>How many cells this command changes.</summary>
    public int cellCount => cells.Count;

    public override bool IsNoOp => IsEmpty;

    /// <summary>
    /// Records a change to one cell. If the cell was already changed by this command (e.g. during a drag),
    /// the ORIGINAL "before" is kept so undo restores the true starting state, and the cell is dropped
    /// entirely if it ended up back where it started.
    /// </summary>
    public void Set(Vector2I cell, TileSnapshot before, TileSnapshot after)
    {
        if (cells.TryGetValue(cell, out var existing))
        {
            before = existing.Before;
        }
        if (before == after)
        {
            cells.Remove(cell);
            return;
        }
        cells[cell] = (before, after);
    }

    protected override void DoCore()
    {
        foreach (var (cell, state) in cells)
        {
            state.After.ApplyTo(map, cell);
        }
    }

    protected override void UndoCore()
    {
        foreach (var (cell, state) in cells)
        {
            state.Before.ApplyTo(map, cell);
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
        public Node2D Node = null!;
        public Node Parent = null!;
        public Vector2 GlobalPos;
        public int Index = -1;
    }

    List<Entry> entries = new();
    bool adds;

    ObjectAttachCommand(bool adds)
    {
        this.adds = adds;
    }

    public override bool IsNoOp => entries.Count == 0;

    /// <summary>A command that adds <paramref name="node"/> under <paramref name="parent"/> at a world position.</summary>
    public static ObjectAttachCommand Add(Node parent, Node2D node, Vector2 globalPos)
    {
        var command = new ObjectAttachCommand(true);
        command.entries.Add(new Entry { Node = node, Parent = parent, GlobalPos = globalPos });
        return command;
    }

    /// <summary>A command that removes every node that still has a parent. Nodes without a parent are skipped.</summary>
    public static ObjectAttachCommand Remove(IEnumerable<Node2D> nodes)
    {
        var command = new ObjectAttachCommand(false);
        foreach (Node2D node in nodes)
        {
            Node? parent = node.GetParent();
            if (parent == null)
            {
                continue;
            }
            command.entries.Add(new Entry
            {
                Node = node,
                Parent = parent,
                GlobalPos = node.GlobalPosition,
                Index = node.GetIndex()
            });
        }
        return command;
    }

    protected override void DoCore()
    {
        if (adds)
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
        if (adds)
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
        // Lowest index first, so each node lands in its original slot.
        var ordered = new List<Entry>();
        foreach (Entry entry in entries)
        {
            if (!GodotObject.IsInstanceValid(entry.Node) || !GodotObject.IsInstanceValid(entry.Parent))
            {
                continue;
            }
            if (entry.Node.GetParent() != null)
            {
                continue;
            }
            ordered.Add(entry);
        }
        ordered.Sort((a, b) => a.Index.CompareTo(b.Index));
        foreach (Entry entry in ordered)
        {
            entry.Parent.AddChild(entry.Node);
            if (entry.Index >= 0 && entry.Index < entry.Parent.GetChildCount())
            {
                entry.Parent.MoveChild(entry.Node, entry.Index);
            }
            // Set after AddChild so the parent's own transform is taken into account.
            entry.Node.GlobalPosition = entry.GlobalPos;
        }
    }

    void Detach()
    {
        // Highest index first, so removing one node doesn't shift the indices recorded for the others.
        var ordered = new List<Entry>();
        foreach (Entry entry in entries)
        {
            if (!GodotObject.IsInstanceValid(entry.Node) || entry.Node.GetParent() != entry.Parent)
            {
                continue;
            }
            ordered.Add(entry);
        }
        ordered.Sort((a, b) => b.Node.GetIndex().CompareTo(a.Node.GetIndex()));
        foreach (Entry entry in ordered)
        {
            entry.Index = entry.Node.GetIndex();
            entry.GlobalPos = entry.Node.GlobalPosition;
            entry.Parent.RemoveChild(entry.Node);
        }
    }

    /// <summary>Frees nodes that are detached (and therefore owned only by this command).</summary>
    public override void Dispose()
    {
        foreach (Entry entry in entries)
        {
            bool detached = GodotObject.IsInstanceValid(entry.Node) && entry.Node.GetParent() == null;
            if (detached && !entry.Node.IsQueuedForDeletion())
            {
                entry.Node.QueueFree();
            }
        }
    }
}

/// <summary>Moves placed objects and keeps their stored cell in sync.</summary>
public sealed class ObjectMoveCommand : EditorCommand
{
    /// <summary>One object's move: world position and grid cell before and after.</summary>
    public record struct Entry(Node2D Node, Vector2 FromPos, Vector2 ToPos, Vector2I FromCell, Vector2I ToCell);

    List<Entry> entries;

    public ObjectMoveCommand(List<Entry> entries)
    {
        this.entries = entries;
    }

    public override bool IsNoOp => entries.Count == 0;

    protected override void DoCore()
    {
        foreach (Entry entry in entries)
        {
            Apply(entry.Node, entry.ToPos, entry.ToCell);
        }
    }

    protected override void UndoCore()
    {
        foreach (Entry entry in entries)
        {
            Apply(entry.Node, entry.FromPos, entry.FromCell);
        }
    }

    static void Apply(Node2D node, Vector2 position, Vector2I cell)
    {
        if (!GodotObject.IsInstanceValid(node))
        {
            return;
        }
        node.SetMeta(EditorObject.metaCell, cell);
        // A detached node (deleted, then history replayed in a different order) is not in the tree,
        // and setting GlobalPosition on it raises an engine error.
        if (!node.IsInsideTree())
        {
            return;
        }
        node.GlobalPosition = position;
    }
}

// ======================================================================
// Utility commands
// ======================================================================

/// <summary>Several commands that undo and redo as one. Undo runs them in reverse order.</summary>
public sealed class CompositeCommand : EditorCommand
{
    List<EditorCommand> commands;

    public CompositeCommand(List<EditorCommand> commands)
    {
        this.commands = commands;
    }

    public override bool IsNoOp => commands.TrueForAll(command => command.IsNoOp);

    protected override void DoCore()
    {
        foreach (EditorCommand command in commands)
        {
            command.Do();
        }
    }

    protected override void UndoCore()
    {
        for (int i = commands.Count - 1; i >= 0; i--)
        {
            commands[i].Undo();
        }
    }

    public override void Dispose()
    {
        foreach (EditorCommand command in commands)
        {
            command.Dispose();
        }
    }
}

/// <summary>Undoable wrapper around two delegates. Handy for item-specific context menu options.</summary>
public sealed class ActionCommand : EditorCommand
{
    Action doAction;
    Action undoAction;

    public ActionCommand(Action doIt, Action undoIt)
    {
        doAction = doIt;
        undoAction = undoIt;
    }

    protected override void DoCore()
    {
        doAction();
    }

    protected override void UndoCore()
    {
        undoAction();
    }
}