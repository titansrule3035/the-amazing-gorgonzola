using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What's under the cursor: a placed object, or (Object == null) a tile.</summary>
public readonly record struct Pick(Vector2I Cell, Node2D? Object)
{
    public bool IsTile => Object == null;
}

/// <summary>
/// The current selection of tiles and placed objects, plus everything that operates on it
/// (picking, rectangle select, move). Respects EditorObject's selection filter.
/// </summary>
public sealed class EditorSelection
{
    readonly EditorObject _e;

    public readonly HashSet<Vector2I> Tiles = new();
    public readonly HashSet<Node2D> Objects = new();

    public event Action? Changed;

    public EditorSelection(EditorObject editor) => _e = editor;

    public bool IsEmpty => Tiles.Count == 0 && Objects.Count == 0;
    public int Count => Tiles.Count + Objects.Count;

    void Notify() => Changed?.Invoke();

    // ------------------------------------------------------------------
    // Basic operations
    // ------------------------------------------------------------------
    public void Clear()
    {
        if (IsEmpty) return;

        Tiles.Clear();
        Objects.Clear();
        Notify();
    }

    public void Set(IEnumerable<Vector2I> tiles, IEnumerable<Node2D> objects)
    {
        Tiles.Clear();
        Objects.Clear();

        foreach (var c in tiles) Tiles.Add(c);

        foreach (var o in objects)
        {
            if (GodotObject.IsInstanceValid(o)) Objects.Add(o);
        }

        Notify();
    }

    public void SelectOnly(Pick pick)
    {
        Tiles.Clear();
        Objects.Clear();

        if (pick.Object != null) Objects.Add(pick.Object);
        else Tiles.Add(pick.Cell);

        Notify();
    }

    public bool Contains(Pick pick) =>
        pick.Object != null ? Objects.Contains(pick.Object) : Tiles.Contains(pick.Cell);

    /// <summary>True if a selected tile or object occupies this cell.</summary>
    public bool HitsSelection(Vector2I cell)
    {
        if (Tiles.Contains(cell)) return true;

        foreach (var o in Objects)
        {
            if (GodotObject.IsInstanceValid(o) && _e.GetObjectCell(o) == cell) return true;
        }

        return false;
    }

    /// <summary>Drops anything that no longer exists (after undo, erase, etc).</summary>
    public void Prune()
    {
        int before = Count;

        Tiles.RemoveWhere(c => _e.TileMap.GetCellSourceId(c) == -1);
        Objects.RemoveWhere(o => !GodotObject.IsInstanceValid(o) || !o.IsInsideTree());

        if (Count != before) Notify();
    }

    // ------------------------------------------------------------------
    // Picking and selecting
    // ------------------------------------------------------------------

    /// <summary>Topmost object at the cell (that passes the filter), else the tile.</summary>
    public bool TryPick(Vector2I cell, out Pick pick)
    {
        var objects = _e.ObjectsAtCell(cell);

        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (_e.PassesFilter(objects[i]))
            {
                pick = new Pick(cell, objects[i]);
                return true;
            }
        }

        if (_e.TilesPassFilter && _e.TileMap.GetCellSourceId(cell) != -1)
        {
            pick = new Pick(cell, null);
            return true;
        }

        pick = default;
        return false;
    }

    public void SelectRect(Vector2I a, Vector2I b, bool additive)
    {
        if (!additive)
        {
            Tiles.Clear();
            Objects.Clear();
        }

        int minX = Math.Min(a.X, b.X);
        int maxX = Math.Max(a.X, b.X);
        int minY = Math.Min(a.Y, b.Y);
        int maxY = Math.Max(a.Y, b.Y);

        if (_e.TilesPassFilter)
        {
            foreach (Vector2I c in _e.TileMap.GetUsedCells())
            {
                if (c.X >= minX && c.X <= maxX && c.Y >= minY && c.Y <= maxY)
                {
                    Tiles.Add(c);
                }
            }
        }

        foreach (var obj in _e.PlaceableObjects())
        {
            if (!_e.PassesFilter(obj)) continue;

            Vector2I c = _e.GetObjectCell(obj);

            if (c.X >= minX && c.X <= maxX && c.Y >= minY && c.Y <= maxY)
            {
                Objects.Add(obj);
            }
        }

        Notify();
    }

    public void SelectAllOfType(EditorItemObject.ItemType type)
    {
        Tiles.Clear();
        Objects.Clear();

        foreach (var obj in _e.PlaceableObjects())
        {
            if (_e.GetObjectType(obj) == type) Objects.Add(obj);
        }

        Notify();
    }

    // ------------------------------------------------------------------
    // Moving
    // ------------------------------------------------------------------

    /// <summary>Cells occupied by placed objects that are NOT part of the selection.</summary>
    public HashSet<Vector2I> OtherObjectCells()
    {
        var cells = new HashSet<Vector2I>();

        foreach (var obj in _e.PlaceableObjects())
        {
            if (!Objects.Contains(obj)) cells.Add(_e.GetObjectCell(obj));
        }

        return cells;
    }

    /// <summary>A move is valid if nothing outside the selection is in the way.</summary>
    public bool CanMove(Vector2I delta, HashSet<Vector2I> otherObjectCells)
    {
        var tileDestinations = new HashSet<Vector2I>();
        foreach (var c in Tiles) tileDestinations.Add(c + delta);

        foreach (var c in Tiles)
        {
            Vector2I d = c + delta;

            bool blockedByTile = !Tiles.Contains(d) && _e.TileMap.GetCellSourceId(d) != -1;
            if (blockedByTile || otherObjectCells.Contains(d)) return false;
        }

        foreach (var obj in Objects)
        {
            if (!GodotObject.IsInstanceValid(obj)) continue;

            Vector2I d = _e.GetObjectCell(obj) + delta;

            bool blockedByTile = !Tiles.Contains(d) && _e.TileMap.GetCellSourceId(d) != -1;
            if (blockedByTile || otherObjectCells.Contains(d) || tileDestinations.Contains(d)) return false;
        }

        return true;
    }

    /// <summary>Moves the whole selection by a cell offset as one undo step.</summary>
    public bool TryMove(Vector2I delta)
    {
        if (delta == Vector2I.Zero || IsEmpty) return false;
        if (!CanMove(delta, OtherObjectCells())) return false;

        var commands = new List<EditorCommand>();
        var map = _e.TileMap;

        var originalTiles = Tiles.ToList();
        var shiftedTiles = originalTiles.Select(c => c + delta).ToList();
        var objects = Objects.Where(GodotObject.IsInstanceValid).ToList();

        // --- tiles ---
        if (originalTiles.Count > 0)
        {
            var before = new Dictionary<Vector2I, TileSnapshot>();
            foreach (var c in originalTiles) before[c] = TileSnapshot.Capture(map, c);
            foreach (var c in originalTiles) before.TryAdd(c + delta, TileSnapshot.Capture(map, c + delta));

            var after = new Dictionary<Vector2I, TileSnapshot>(before);
            foreach (var c in originalTiles) after[c] = TileSnapshot.Empty;
            foreach (var c in originalTiles) after[c + delta] = before[c];

            var tileCommand = new TileChangeCommand(map);
            foreach (var (cell, snapshot) in before) tileCommand.Set(cell, snapshot, after[cell]);

            commands.Add(tileCommand);
        }

        // --- objects ---
        if (objects.Count > 0)
        {
            Vector2 step = _e.CellToWorld(delta) - _e.CellToWorld(Vector2I.Zero);
            var entries = new List<ObjectMoveCommand.Entry>();

            foreach (var obj in objects)
            {
                Vector2I cell = _e.GetObjectCell(obj);
                entries.Add(new ObjectMoveCommand.Entry(obj, obj.GlobalPosition, obj.GlobalPosition + step, cell, cell + delta));
            }

            commands.Add(new ObjectMoveCommand(entries));
        }

        var composite = new CompositeCommand(commands);
        composite.OnApplied = undo => Set(undo ? originalTiles : shiftedTiles, objects);

        _e.History.Execute(composite);
        return true;
    }
}