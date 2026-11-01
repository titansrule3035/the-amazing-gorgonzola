using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What's under the cursor: a placed object, or a tile when <paramref name="Object"/> is null.</summary>
/// <param name="Cell">The grid cell that was picked.</param>
/// <param name="Object">The placed object at that cell, or null when the pick is a tile.</param>
public record struct Pick(Vector2I Cell, Node2D? Object)
{
    /// <summary>True when the pick refers to a tile rather than a placed object.</summary>
    public bool IsTile => Object == null;
}

/// <summary>
/// The current selection of tiles and placed objects, plus everything that operates on it
/// (picking, rectangle select, move). Respects EditorObject's selection filter.
/// Change the sets through the methods on this class so <see cref="Changed"/> fires.
/// </summary>
public sealed class EditorSelection
{
    EditorObject editor;

    /// <summary>Selected tile cells.</summary>
    public HashSet<Vector2I> tiles { get; private set; } = new();

    /// <summary>Selected placed objects. May briefly contain freed nodes; <see cref="Prune"/> removes them.</summary>
    public HashSet<Node2D> objects { get; private set; } = new();

    /// <summary>Raised whenever the selection changes.</summary>
    public event Action? Changed;

    public EditorSelection(EditorObject editor)
    {
        this.editor = editor;
    }

    public bool isEmpty => tiles.Count == 0 && objects.Count == 0;

    public int count => tiles.Count + objects.Count;

    void Notify()
    {
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------
    // Basic operations
    // ------------------------------------------------------------------

    /// <summary>Deselects everything. Does nothing (and raises nothing) if already empty.</summary>
    public void Clear()
    {
        if (isEmpty)
        {
            return;
        }
        tiles.Clear();
        objects.Clear();
        Notify();
    }

    /// <summary>Replaces the selection. Freed objects are skipped. The arguments may be this selection's own sets.</summary>
    public void Set(IEnumerable<Vector2I> newTiles, IEnumerable<Node2D> newObjects)
    {
        // Copy first: clearing below would empty the arguments if they are the live sets.
        List<Vector2I> tileList = newTiles.ToList();
        List<Node2D> objectList = newObjects.ToList();
        tiles.Clear();
        objects.Clear();
        foreach (Vector2I cell in tileList)
        {
            tiles.Add(cell);
        }
        foreach (Node2D obj in objectList)
        {
            if (GodotObject.IsInstanceValid(obj))
            {
                objects.Add(obj);
            }
        }
        Notify();
    }

    /// <summary>Replaces the selection with just the picked object or tile.</summary>
    public void SelectOnly(Pick pick)
    {
        tiles.Clear();
        objects.Clear();
        if (pick.Object != null)
        {
            objects.Add(pick.Object);
        }
        else
        {
            tiles.Add(pick.Cell);
        }
        Notify();
    }

    public bool Contains(Pick pick)
    {
        return pick.Object != null ? objects.Contains(pick.Object) : tiles.Contains(pick.Cell);
    }

    /// <summary>True if a selected tile or object occupies this cell.</summary>
    public bool HitsSelection(Vector2I cell)
    {
        if (tiles.Contains(cell))
        {
            return true;
        }
        foreach (Node2D obj in objects)
        {
            if (GodotObject.IsInstanceValid(obj) && editor.GetObjectCell(obj) == cell)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Drops anything that no longer exists (after undo, erase, etc).</summary>
    public void Prune()
    {
        int before = count;
        tiles.RemoveWhere(cell => editor.tileMap.GetCellSourceId(cell) == -1);
        objects.RemoveWhere(obj => !GodotObject.IsInstanceValid(obj) || !obj.IsInsideTree());
        if (count != before)
        {
            Notify();
        }
    }

    // ------------------------------------------------------------------
    // Picking and selecting
    // ------------------------------------------------------------------

    /// <summary>Topmost object at the cell (that passes the filter), else the tile (if tiles pass the filter).</summary>
    /// <returns>False when nothing pickable is at the cell. <paramref name="pick"/> is then default.</returns>
    public bool TryPick(Vector2I cell, out Pick pick)
    {
        List<Node2D> candidates = editor.ObjectsAtCell(cell);
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (editor.PassesFilter(candidates[i]))
            {
                pick = new Pick(cell, candidates[i]);
                return true;
            }
        }
        if (editor.TilesPassFilter && editor.tileMap.GetCellSourceId(cell) != -1)
        {
            pick = new Pick(cell, null);
            return true;
        }
        pick = default;
        return false;
    }

    /// <summary>Selects every tile and object (that passes the filter) inside the rectangle spanned by two cells.</summary>
    /// <param name="additive">True to add to the current selection instead of replacing it.</param>
    public void SelectRect(Vector2I a, Vector2I b, bool additive)
    {
        if (!additive)
        {
            tiles.Clear();
            objects.Clear();
        }
        var min = new Vector2I(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
        var max = new Vector2I(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        if (editor.TilesPassFilter)
        {
            foreach (Vector2I cell in editor.tileMap.GetUsedCells())
            {
                if (InRect(cell, min, max))
                {
                    tiles.Add(cell);
                }
            }
        }
        foreach (Node2D obj in editor.PlaceableObjects())
        {
            if (!editor.PassesFilter(obj))
            {
                continue;
            }
            if (InRect(editor.GetObjectCell(obj), min, max))
            {
                objects.Add(obj);
            }
        }
        Notify();
    }

    static bool InRect(Vector2I cell, Vector2I min, Vector2I max)
    {
        return cell.X >= min.X && cell.X <= max.X && cell.Y >= min.Y && cell.Y <= max.Y;
    }

    /// <summary>Selects every placed object of one type. Ignores the selection filter and selects no tiles.</summary>
    public void SelectAllOfType(EditorItemObject.ItemType type)
    {
        tiles.Clear();
        objects.Clear();
        foreach (Node2D obj in editor.PlaceableObjects())
        {
            if (editor.GetObjectType(obj) == type)
            {
                objects.Add(obj);
            }
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
        foreach (Node2D obj in editor.PlaceableObjects())
        {
            if (!objects.Contains(obj))
            {
                cells.Add(editor.GetObjectCell(obj));
            }
        }
        return cells;
    }

    /// <summary>A move is valid if nothing outside the selection is in the way.</summary>
    /// <param name="delta">Offset in cells.</param>
    /// <param name="otherObjectCells">Result of <see cref="OtherObjectCells"/>. Pass a cached copy while dragging.</param>
    public bool CanMoveSelection(Vector2I delta, HashSet<Vector2I> otherObjectCells)
    {
        var objectDestinations = new HashSet<Vector2I>();
        foreach (Node2D obj in objects)
        {
            if (GodotObject.IsInstanceValid(obj))
            {
                objectDestinations.Add(editor.GetObjectCell(obj) + delta);
            }
        }

        // Tiles: blocked by tiles and objects outside the selection, and by a selected
        // object arriving on a tile that has a collider.
        foreach (Vector2I cell in tiles)
        {
            Vector2I destination = cell + delta;
            bool blockedByTile = !tiles.Contains(destination) && editor.tileMap.GetCellSourceId(destination) != -1;
            bool blockedByMovingObject = objectDestinations.Contains(destination) && editor.CellHasCollider(cell);
            if (blockedByTile || blockedByMovingObject || otherObjectCells.Contains(destination))
            {
                return false;
            }
        }

        // Objects: blocked by other objects, and by tiles with a collider that stay behind.
        foreach (Node2D obj in objects)
        {
            if (!GodotObject.IsInstanceValid(obj))
            {
                continue;
            }
            Vector2I destination = editor.GetObjectCell(obj) + delta;
            bool blockedByTile = !tiles.Contains(destination) && editor.CellHasCollider(destination);
            if (blockedByTile || otherObjectCells.Contains(destination))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Moves the whole selection by a cell offset as one undo step.</summary>
    /// <returns>False if there was nothing to move or the move is blocked.</returns>
    public bool TryMove(Vector2I delta)
    {
        if (delta == Vector2I.Zero || isEmpty)
        {
            return false;
        }
        if (!CanMoveSelection(delta, OtherObjectCells()))
        {
            return false;
        }
        var commands = new List<EditorCommand>();
        List<Vector2I> originalTiles = tiles.ToList();
        List<Vector2I> shiftedTiles = originalTiles.Select(cell => cell + delta).ToList();
        List<Node2D> selectedObjects = objects.Where(GodotObject.IsInstanceValid).ToList();

        // --- tiles ---
        // Autotile aware: also re-tiles the neighbours of the old and new positions, and records them for undo.
        if (originalTiles.Count > 0)
        {
            TileChangeCommand? tileCommand = editor.BuildMoveCommand(originalTiles, delta);
            if (tileCommand != null)
            {
                commands.Add(tileCommand);
            }
        }

        // --- objects ---
        if (selectedObjects.Count > 0)
        {
            Vector2 step = editor.CellToWorld(delta) - editor.CellToWorld(Vector2I.Zero);
            var entries = new List<ObjectMoveCommand.Entry>();
            foreach (Node2D obj in selectedObjects)
            {
                Vector2I cell = editor.GetObjectCell(obj);
                entries.Add(new ObjectMoveCommand.Entry(obj, obj.GlobalPosition, obj.GlobalPosition + step, cell, cell + delta));
            }
            commands.Add(new ObjectMoveCommand(entries));
        }

        var composite = new CompositeCommand(commands);
        // Keep the selection on the moved items through undo and redo.
        composite.OnApplied = undo => Set(undo ? originalTiles : shiftedTiles, selectedObjects);
        editor.history.Execute(composite);
        return true;
    }
}