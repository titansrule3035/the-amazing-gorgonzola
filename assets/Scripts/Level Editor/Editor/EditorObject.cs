using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TAGLevelBuilder.assets.Scripts.Level_Editor;

public partial class EditorObject : Node2D
{
    // Exported Variables

    [Export] public bool canPlace = false;
    [Export] Node2D level;

    [Export] public PackedScene CurrentItem = null;
    [Export] public Texture2D CurrentTile = null;
    [Export] public Texture2D EraserTexture;

    [Export] public CursorMode currentCursorMode = CursorMode.Place;
    [Export] public CursorMode lastCursorMode = CursorMode.Null;
    [Export] public FilterMode currentFilterMode = FilterMode.All;

    // Node References
    Sprite2D CursorSprite;

    // Cached Nodes
    TileMapLayer tileMapForeground;
    Node highlightedNode;

    // Cached Variables
    Vector2I selectTopLeft;
    Vector2I selectBottomRight;
    private Vector2I _lastPlacedCell = new(int.MinValue, int.MinValue);
    //  - Add array to store selected objects and tiles here
    [Export] Godot.Collections.Array<Vector2I> selectedTiles = new();
    [Export] Godot.Collections.Array<Node2D> selectedObjects = new();

    // Editor State
    public EditorItemObject itemObject;
    public EditorItemObject.ItemType itemType;

    public bool hideCursor = false;

    // Events
    public Action? ItemPlaced;

    // Constants
    const string levelRoot = "level/level_assets";

    // Enums
    public enum CursorMode
    {
        Mouse,
        Select,
        Place,
        Eraser,
        Copy,
        DragCamera,
        Null
    }

    public enum FilterMode
    {
        All,
        Clones,
        Enemies,
        Hazards,
        Tiles,
        LevelMechanics
    }

    public override void _Ready()
    {
        CursorSprite = GetNode<Sprite2D>("Sprite");
        level = GetParent().GetNode<Node2D>("level");
        tileMapForeground = level.GetNode<TileMapLayer>("tiles/Foreground");
        selectTopLeft = selectBottomRight = new();
    }

    public override void _Process(double delta)
    {
        Vector2 mouseGlobal = GetGlobalMousePosition();

        // convert mouse into TileMap local space
        Vector2 local = tileMapForeground.ToLocal(mouseGlobal);

        // convert to cell
        Vector2I cell = tileMapForeground.LocalToMap(local);

        // convert back to world-aligned position (CENTER of tile)
        Vector2 snappedWorld = tileMapForeground.ToGlobal(tileMapForeground.MapToLocal(cell));

        CursorSprite.Visible = !hideCursor;

        var spaceState = GetWorld2D().DirectSpaceState;
        /* physics query to find any objects under the cursor area */
        PhysicsPointQueryParameters2D query = new PhysicsPointQueryParameters2D();
        {
            query.Position = snappedWorld;
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;
        }
        var results = spaceState.IntersectPoint(query);

        if (Input.IsActionJustPressed("mb_right"))
        {
            GD.Print($"{snappedWorld.X}, {snappedWorld.Y}");
        }

        /* Input mapping */
        {

            if (Input.IsActionJustPressed("mouse_mouse"))
            {
                currentCursorMode = CursorMode.Mouse;
            }

            if (Input.IsActionJustPressed("ctrl"))
            {
                if (currentCursorMode != CursorMode.Mouse)
                {
                    lastCursorMode = currentCursorMode;
                }

                currentCursorMode = CursorMode.Mouse;
            }

            if (Input.IsActionJustReleased("ctrl"))
            {
                currentCursorMode = lastCursorMode;
            }

            if (Input.IsActionJustPressed("mouse_select"))
            {
                currentCursorMode = CursorMode.Select;
            }

            if (Input.IsActionJustPressed("mouse_place"))
            {
                SetEditorItem(itemObject);
                currentCursorMode = CursorMode.Place;
            }

            if (Input.IsActionJustPressed("mouse_eraser"))
            {
                currentCursorMode = CursorMode.Eraser;
            }

            if (Input.IsActionJustPressed("mouse_copy"))
            {
                currentCursorMode = CursorMode.Copy;
            }
        }
        if (currentCursorMode == CursorMode.Mouse)
        {
            hideCursor = true;
            GlobalPosition = GetGlobalMousePosition();
        }

        if (currentCursorMode == CursorMode.Select)
        {
            hideCursor = true;

            if (Input.IsActionJustPressed("mb_left"))
            {
                selectTopLeft = cell;
            }

            if (Input.IsActionPressed("mb_left"))
            {
                selectBottomRight = cell;
                QueueRedraw();
            }

            if (Input.IsActionJustReleased("mb_left"))
            {
                selectBottomRight = cell;
                SelectObjects();
                QueueRedraw();
            }
        }

        if (currentCursorMode == CursorMode.Place)
        {
            // fix alignment, the cursor should be centered on the object (if not tile) by the BOTTOM of its sprite

            float offset = 0f;

            if (CursorSprite.Texture != null)
            {
                offset = CursorSprite.GetRect().Size.Y / 2;
            }

            GlobalPosition = snappedWorld + new Vector2(0, 16);
            CursorSprite.GlobalPosition = GlobalPosition - new Vector2(0, offset);

            if ((CurrentItem != null || CurrentTile != null) && itemObject != null && GetTree().Paused)
            {
                if (itemObject.disabled)
                {
                    SetTexture(new(), false);
                    itemType = EditorItemObject.ItemType.Null;
                    CurrentItem = null;
                }

                if (itemType == EditorItemObject.ItemType.Null || results.Count != 0)
                {
                    foreach (Godot.Collections.Dictionary result in results)
                    {
                        return;
                    }
                }
                else if (tileMapForeground.GetCellSourceId(cell) != -1)
                {
                    return;
                }

                if (canPlace)
                {
                    if (Input.IsActionPressed("mb_left"))
                    {
                        if (cell != _lastPlacedCell)
                        {
                            _lastPlacedCell = cell;
                        }
                        else
                        {
                            return;
                        }

                        ItemPlaced?.Invoke();

                        if (itemType == EditorItemObject.ItemType.Tile)
                        {
                            EditorTileObject newTile = (EditorTileObject)itemObject;
                            Vector2I atlasCoords = newTile.GetAtlasCoords();
                            tileMapForeground.SetCell(cell, newTile.tileID, atlasCoords);
                        }
                        else
                        {
                            Node2D NewItem = CurrentItem.Instantiate<Node2D>();
                            Node2D Parent = GetTree().CurrentScene.GetNode<Node2D>($"{levelRoot}/{itemObject.GetGroupDestination()}");
                            NewItem.GlobalPosition = new Vector2(snappedWorld.X, snappedWorld.Y + 16 + itemObject.cursorOffset.Y);
                            NewItem.AddToGroup("editor_placeable");
                            Parent.AddChild(NewItem);
                        }
                    }
                }
            }
            else
            {
                hideCursor = true;
            }
        }

        if (currentCursorMode == CursorMode.Eraser)
        {
            hideCursor = false;
            CursorSprite.Texture = EraserTexture;
            GlobalPosition = GetGlobalMousePosition();
            if (canPlace && Input.IsActionPressed("mb_left"))
            {
                // erase the tile at the current cell position, if any
                tileMapForeground.EraseCell(cell);

                //DEBUG: list result count
                GD.Print(results.Count);

                // erase any objects touching the cursor area, if any
                foreach (Godot.Collections.Dictionary result in results)
                {
                    Node collider = result["collider"].As<Node>();

                    if (collider == null)
                        continue;

                    GD.Print($"Hit: {collider.Name} ({collider.GetType().Name})");

                    Node current = collider;

                    while (current != null)
                    {
                        if (current.IsInGroup("editor_placeable"))
                        {
                            GD.Print($"Erased {current.Name}");
                            current.QueueFree();
                            break;
                        }

                        current = current.GetParent();
                    }
                }
            }
        }

        if (currentCursorMode == CursorMode.Copy)
        {
            hideCursor = true;
        }

        if (currentCursorMode == CursorMode.DragCamera)
        {
            hideCursor = true;
        }
        else
        {
            hideCursor = false;
        }
    }

    public override void _Draw()
    {

        if (currentCursorMode == CursorMode.Select && Input.IsActionPressed("mb_left"))
        {
            Vector2I minCell = new(Mathf.Min(selectTopLeft.X, selectBottomRight.X), Mathf.Min(selectTopLeft.Y, selectBottomRight.Y));

            Vector2I maxCell = new(Mathf.Max(selectTopLeft.X, selectBottomRight.X), Mathf.Max(selectTopLeft.Y, selectBottomRight.Y));

            Vector2 tileSize = tileMapForeground.TileSet.TileSize;

            Vector2 worldTopLeft = tileMapForeground.ToGlobal(tileMapForeground.MapToLocal(minCell)) - tileSize / 2;

            Vector2 worldBottomRight = tileMapForeground.ToGlobal(tileMapForeground.MapToLocal(maxCell)) + tileSize / 2;

            Rect2 rect = new Rect2(
                Mathf.Min(worldTopLeft.X, worldBottomRight.X) - GlobalPosition.X,
                Mathf.Min(worldTopLeft.Y, worldBottomRight.Y) - GlobalPosition.Y,
                Mathf.Abs(worldBottomRight.X - worldTopLeft.X),
                Mathf.Abs(worldBottomRight.Y - worldTopLeft.Y)
            );

            DrawRect(rect, new Color(0.3f, 0.6f, 1f, 0.25f), filled: true);
            DrawRect(rect, new Color(0.3f, 0.6f, 1f, 0.9f), filled: false, width: 1.5f);
        }
    }

    void SetTexture(Texture2D texture, bool flipH)
    {
        CursorSprite.Texture = texture;

        CursorSprite.FlipH = flipH;
    }

    public void SetEditorItem(EditorItemObject editorItem)
    {
        if (editorItem == null)
        {
            GD.PrintErr("EditorObject.SetEditorItem: editorItem is null");
            return;
        }

        itemObject = editorItem;
        itemType = editorItem.itemType;

        if (editorItem.itemType == EditorItemObject.ItemType.Tile)
        {
            EditorTileObject tileObject = editorItem as EditorTileObject;
            SetTexture(tileObject.GetTileTexture(), false);
            SetCurrentTile(tileObject.Texture);
        }
        else
        {
            SetTexture(editorItem.Texture, editorItem.FlipH);

            if (editorItem.itemType == EditorItemObject.ItemType.Clone)
            {
                CursorSprite.Material = editorItem.Material;
            }

            SetCurrentScene(editorItem.ThisScene, editorItem.itemType);
        }

        CursorSprite.Modulate = new Color(1, 1, 1, 0.5f); // 50% transparent
    }

    public void SetCurrentTile(Texture2D tile)
    {
        CurrentItem = null;
        CurrentTile = tile;
    }

    void SetCurrentScene(PackedScene newScene, EditorItemObject.ItemType itemType)
    {
        CurrentTile = null;
        CurrentItem = newScene;
    }

    public void SetCursorMode(CursorMode mode)
    {
        currentCursorMode = mode;
    }

    private void SelectObjects()
    {
        selectedTiles.Clear();
        selectedObjects.Clear();

        // normalize so topLeft/bottomRight are actually top-left/bottom-right
        int minX = Math.Min(selectTopLeft.X, selectBottomRight.X);
        int maxX = Math.Max(selectTopLeft.X, selectBottomRight.X);
        int minY = Math.Min(selectTopLeft.Y, selectBottomRight.Y);
        int maxY = Math.Max(selectTopLeft.Y, selectBottomRight.Y);

        // --- tiles ---
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2I cellPos = new(x, y);
                if (tileMapForeground.GetCellSourceId(cellPos) != -1)
                {
                    selectedTiles.Add(cellPos);
                }
            }
        }

        // --- objects (clones, hazards, etc via "editor_placeable" group) ---
        foreach (Node node in GetTree().GetNodesInGroup("editor_placeable"))
        {
            if (node is Node2D obj)
            {
                Vector2 localToTilemap = tileMapForeground.ToLocal(obj.GlobalPosition);
                Vector2I objCell = tileMapForeground.LocalToMap(localToTilemap);

                if (objCell.X >= minX && objCell.X <= maxX &&
                    objCell.Y >= minY && objCell.Y <= maxY)
                {
                    selectedObjects.Add(obj);
                }
            }
        }

        GD.Print($"Selected {selectedTiles.Count} tiles, {selectedObjects.Count} objects");
    }
}
