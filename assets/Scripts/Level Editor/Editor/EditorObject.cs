using Godot;
using System;
using System.Text.RegularExpressions;
using TAGLevelBuilder.assets.Scripts.Level_Editor;

public partial class EditorObject : Node2D
{
    [Export] public bool canPlace = false;
    [Export] Node2D level;
    [Export] public PackedScene CurrentItem = null;
    [Export] public Texture2D CurrentTile = null;
    [Export] EditorItemObject.ItemType itemType;
    Sprite2D CursorSprite;
    EditorItemObject itemObject;
    public bool hideCursor = false;
    public Action? ItemPlaced;

    TileMapLayer tileMapForeground;


    string levelRoot = "level/level_assets";


    public override void _Ready()
    {
        CursorSprite = GetNode<Sprite2D>("Sprite");
        level = GetParent().GetNode<Node2D>("level");
        tileMapForeground = GetParent().GetNode<TileMapLayer>("level/tiles/Foreground");
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

        // apply to cursor
        GlobalPosition = snappedWorld;

        CursorSprite.Visible = !hideCursor;

        Vector2 cursorOffset = Vector2.Zero;

        if(itemType == EditorItemObject.ItemType.Clone)
        {
            cursorOffset = new(0, 10.076f);
        }

        CursorSprite.Position = cursorOffset;

        if ((CurrentItem != null || CurrentTile != null) && GetTree().Paused)
        {
            if (itemObject.disabled)
            {
                SetTexture(new(), false);
                itemType = EditorItemObject.ItemType.Null;
                CurrentItem = null;
            }
            if (canPlace && Input.IsActionJustPressed("mb_left"))
            {
                ItemPlaced?.Invoke();
                if (itemType == EditorItemObject.ItemType.Null)
                {
                    return;
                }
                else if (itemType == EditorItemObject.ItemType.Tile)
                {
                    EditorTileObject tileObject = (EditorTileObject)itemObject;

                    Vector2I atlasCoords = tileObject.GetAtlasCoords();

                    tileMapForeground.SetCell(cell, tileObject.tileID, atlasCoords);

                    return;
                }
                else
                {
                    Node2D NewItem = CurrentItem.Instantiate<Node2D>();
                    Node2D parent = GetTree().CurrentScene.GetNode<Node2D>($"{levelRoot}/clones");
                    Vector2 spawnPos = snappedWorld;
                    if (itemType == EditorItemObject.ItemType.Clone)
                    {
                        spawnPos = new(spawnPos.X, spawnPos.Y + (CursorSprite.Texture.GetHeight() / 2) + 10.076f);
                    }
                    else
                    {
                        spawnPos = new(spawnPos.X, spawnPos.Y);
                    }
                    NewItem.GlobalPosition = spawnPos;
                    GetTree().CurrentScene.GetNode<Node2D>($"{levelRoot}/{itemObject.GetGroupDestination()}").AddChild(NewItem);
                    return;
                }
            }
            else if (canPlace && Input.IsActionPressed("mb_left"))
            {
                if (itemType == EditorItemObject.ItemType.Tile)
                {
                    EditorTileObject tileObject = (EditorTileObject)itemObject;

                    Vector2I atlasCoords = tileObject.GetAtlasCoords();

                    tileMapForeground.SetCell(cell, 2, atlasCoords);

                    return;
                }
            }
        }
        else
        {
            hideCursor = true;
        }
    }

    private void SetTexture(Texture2D texture, bool flipH)
    {
        CursorSprite.Texture = texture;

        CursorSprite.FlipH = flipH;
    }

    public void SetEditorItem(EditorItemObject editorItem)
    {
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
            SetCurrentScene(editorItem.ThisScene, editorItem.itemType);
        }


    }

    public void SetCurrentTile(Texture2D tile)
    {
        CurrentItem = null;
        CurrentTile = tile;
    }
    private void SetCurrentScene(PackedScene newScene, EditorItemObject.ItemType itemType)
    {
        CurrentTile = null;
        CurrentItem = newScene;
    }
}
