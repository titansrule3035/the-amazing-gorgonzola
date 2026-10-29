using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Remembers the state of every tile cell an edit might touch, BEFORE the edit changes it,
/// then builds one TileChangeCommand from the differences.
/// Cells are added with <see cref="Touch"/> as the edit spreads, so changes far from the original
/// cell are still recorded and undo restores them.
/// </summary>
public sealed class TileEditRecorder
{
    TileMapLayer map;
    Dictionary<Vector2I, TileSnapshot> before = new();

    public TileEditRecorder(TileMapLayer map)
    {
        this.map = map;
    }

    /// <summary>Records the current state of cells that have not been recorded yet. Call before changing them.</summary>
    public void Touch(IEnumerable<Vector2I> cells)
    {
        foreach (Vector2I c in cells)
        {
            if (!before.ContainsKey(c))
            {
                before[c] = TileSnapshot.Capture(map, c);
            }
        }
    }

    /// <summary>One command covering every touched cell that actually changed. Null when nothing changed.</summary>
    public TileChangeCommand? Build()
    {
        var command = new TileChangeCommand(map);
        foreach (var (cell, snapshot) in before)
        {
            command.Set(cell, snapshot, TileSnapshot.Capture(map, cell));
        }
        return command.IsEmpty ? null : command;
    }
}

/// <summary>
/// Autotiling for the editor, built on Godot's TileSet terrains (TileMapLayer.SetCellsTerrainConnect).
/// A palette tile autotiles when its atlas tile has a terrain set and a terrain assigned in the TileSet.
///
/// One connect call is not always enough: Godot weighs a cell's neighbours by the tiles they currently show,
/// so a neighbour that was left stale keeps skewing the result. After every edit the code therefore
/// "settles" the area: it re-tiles the surrounding cells, and if any tile changed it re-tiles around
/// those changes too, repeating until nothing changes (or maxSettlePasses is reached).
/// Every cell that changed along the way is recorded, so undo restores all of them.
/// Assumes a square grid. Other grid shapes need a different neighbour ring in CellsAround.
/// </summary>
public partial class EditorObject
{
    /// <summary>Prints what the autotile code decides. Handy while setting up a TileSet.</summary>
    [Export] public bool autotileDebug = false;

    /// <summary>Upper limit on settle passes per edit. A guard against two tiles that keep flipping each other.</summary>
    [Export] public int maxSettlePasses = 8;

    void AutotileLog(string message)
    {
        if (autotileDebug)
        {
            GD.Print("[Autotile] " + message);
        }
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>The terrain an atlas tile belongs to, or null when it has none (a plain tile).</summary>
    public (int Set, int Terrain)? TerrainOfTile(int sourceId, Vector2I atlas)
    {
        if (tileMap.TileSet.GetSource(sourceId) is not TileSetAtlasSource source)
        {
            AutotileLog($"source {sourceId} is not an atlas source, so this is a plain tile.");
            return null;
        }
        if (!source.HasTile(atlas))
        {
            AutotileLog($"source {sourceId} has no tile at atlas {atlas}, so this is a plain tile.");
            return null;
        }
        TileData? data = source.GetTileData(atlas, 0);
        if (data == null || data.TerrainSet < 0 || data.Terrain < 0)
        {
            AutotileLog($"tile {atlas} in source {sourceId} has no terrain (set {data?.TerrainSet}, terrain {data?.Terrain}), so this is a plain tile.");
            return null;
        }
        AutotileLog($"tile {atlas} in source {sourceId} uses terrain set {data.TerrainSet}, terrain {data.Terrain}.");
        return (data.TerrainSet, data.Terrain);
    }

    /// <summary>Paints a terrain onto one cell, then settles the tiles around it. Executes as one command.</summary>
    /// <returns>False when nothing changed.</returns>
    public bool PlaceAutotile(Vector2I target, int terrainSet, int terrain)
    {
        TileChangeCommand? command = RecordTileEdit(new[] { target }, recorder =>
        {
            tileMap.SetCellsTerrainConnect(new Godot.Collections.Array<Vector2I> { target }, terrainSet, terrain);
            Settle(recorder, new[] { target });
        });
        if (command == null)
        {
            AutotileLog($"nothing changed at {target}. The TileSet has {tileMap.TileSet.GetTerrainSetsCount()} terrain set(s). Check the peering bits.");
            return false;
        }
        AutotileLog($"placed terrain {terrain} at {target}: {command.cellCount} cell(s) changed.");
        history.Execute(command);
        return true;
    }

    /// <summary>
    /// Erases tiles and settles the tiles around them so the edges close up.
    /// Returns the command without executing it, so callers can combine it with other commands.
    /// The map is already changed when this returns. Execute the command (or a composite holding it).
    /// </summary>
    /// <returns>Null when none of the cells held a tile.</returns>
    public TileChangeCommand? BuildEraseCommand(IEnumerable<Vector2I> cells)
    {
        var targets = new List<Vector2I>();
        foreach (Vector2I c in cells)
        {
            if (tileMap.GetCellSourceId(c) != -1)
            {
                targets.Add(c);
            }
        }
        // Cheap exit: the eraser calls this every frame, mostly over empty cells.
        if (targets.Count == 0)
        {
            return null;
        }
        return RecordTileEdit(targets, recorder =>
        {
            foreach (Vector2I c in targets)
            {
                tileMap.EraseCell(c);
            }
            Settle(recorder, targets);
        });
    }

    /// <summary>
    /// Moves tiles by a cell offset and settles the tiles around both the old and the new positions.
    /// Returns the command without executing it, like <see cref="BuildEraseCommand"/>.
    /// The caller must already have checked that the move is valid (see EditorSelection.CanMove).
    /// </summary>
    /// <returns>Null when nothing changed.</returns>
    public TileChangeCommand? BuildMoveCommand(IEnumerable<Vector2I> cells, Vector2I delta)
    {
        List<Vector2I> sources = cells.ToList();
        List<Vector2I> destinations = sources.Select(c => c + delta).ToList();
        List<TileSnapshot> moved = sources.Select(c => TileSnapshot.Capture(tileMap, c)).ToList();
        List<Vector2I> touched = sources.Concat(destinations).ToList();
        return RecordTileEdit(touched, recorder =>
        {
            // Empty every source first, then write the destinations, so overlapping moves resolve correctly.
            foreach (Vector2I c in sources)
            {
                tileMap.EraseCell(c);
            }
            for (int i = 0; i < sources.Count; i++)
            {
                moved[i].ApplyTo(tileMap, destinations[i]);
            }
            Settle(recorder, touched);
        });
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    /// <summary>Runs an edit on the tile map and records every cell it changed, however far it spread.</summary>
    /// <param name="edited">The cells the edit is aimed at. They and their neighbours are recorded up front.</param>
    /// <param name="edit">The edit itself, applied to the map immediately. Use the recorder when it spreads.</param>
    /// <returns>Null when the edit changed nothing.</returns>
    TileChangeCommand? RecordTileEdit(IEnumerable<Vector2I> edited, Action<TileEditRecorder> edit)
    {
        var recorder = new TileEditRecorder(tileMap);
        recorder.Touch(CellsAround(edited));
        edit(recorder);
        return recorder.Build();
    }

    /// <summary>
    /// Re-tiles the area around the seed cells, then keeps re-tiling around anything that changed,
    /// until a pass changes nothing or <see cref="maxSettlePasses"/> is reached.
    /// </summary>
    void Settle(TileEditRecorder recorder, IEnumerable<Vector2I> seeds)
    {
        HashSet<Vector2I> work = CellsAround(seeds);
        int pass = 0;
        while (work.Count > 0 && pass < maxSettlePasses)
        {
            pass++;
            // Godot may also adjust the neighbours of the cells we pass in, so watch (and record) one ring further out.
            HashSet<Vector2I> watched = CellsAround(work);
            recorder.Touch(watched);
            var beforePass = new Dictionary<Vector2I, TileSnapshot>();
            foreach (Vector2I c in watched)
            {
                beforePass[c] = TileSnapshot.Capture(tileMap, c);
            }
            Retile(work);
            var changed = new List<Vector2I>();
            foreach (Vector2I c in watched)
            {
                if (TileSnapshot.Capture(tileMap, c) != beforePass[c])
                {
                    changed.Add(c);
                }
            }
            AutotileLog($"settle pass {pass}: {changed.Count} cell(s) changed.");
            work = CellsAround(changed);
        }
        if (work.Count > 0)
        {
            AutotileLog($"settle stopped at the pass limit ({maxSettlePasses}). Some tiles may still be flipping.");
        }
    }

    /// <summary>Re-evaluates the tile of every autotile cell in the list. Plain tiles and empty cells are skipped.</summary>
    void Retile(IEnumerable<Vector2I> cells)
    {
        var groups = new Dictionary<(int Set, int Terrain), Godot.Collections.Array<Vector2I>>();
        foreach (Vector2I c in cells)
        {
            TileData? data = tileMap.GetCellTileData(c);
            if (data == null || data.TerrainSet < 0 || data.Terrain < 0)
            {
                continue;
            }
            var key = (data.TerrainSet, data.Terrain);
            if (!groups.TryGetValue(key, out Godot.Collections.Array<Vector2I>? list))
            {
                list = new Godot.Collections.Array<Vector2I>();
                groups[key] = list;
            }
            list.Add(c);
        }
        foreach (var (key, list) in groups)
        {
            tileMap.SetCellsTerrainConnect(list, key.Set, key.Terrain);
        }
    }

    /// <summary>The cells themselves plus their eight neighbours.</summary>
    static HashSet<Vector2I> CellsAround(IEnumerable<Vector2I> cells)
    {
        var around = new HashSet<Vector2I>();
        foreach (Vector2I c in cells)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    around.Add(c + new Vector2I(x, y));
                }
            }
        }
        return around;
    }
}