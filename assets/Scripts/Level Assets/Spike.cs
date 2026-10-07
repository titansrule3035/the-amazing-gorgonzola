using Godot;
using GodotPlugins.Game;
using System;

public partial class Spike : Node2D
{
    public override void _Ready()
    {
        Node levelRoot = levelRoot = GetTree().CurrentScene.GetNode<Node2D>(((Runtime)GetTree().CurrentScene).editorMode ? "editor/main" : "game/main");

        TileMapLayer tileMapForeground = levelRoot.GetNode<TileMapLayer>("level/tiles/Foreground");

        Vector2 local = tileMapForeground.ToLocal(GlobalPosition);

        Vector2I cell = tileMapForeground.LocalToMap(local);

        Vector2 snappedWorld = tileMapForeground.ToGlobal(tileMapForeground.MapToLocal(cell));

        Vector2I atlasCoords = GetAtlasCoords();

        tileMapForeground.SetCell(new(cell.X, cell.Y - 1), 1, atlasCoords);

        Visible = false;
    }

    public Vector2I GetAtlasCoords()
    {
        Vector2I coords = Vector2I.Zero;

        Vector2 precoords = (GetNode<Sprite2D>("sprite").Texture as AtlasTexture).Region.Position;

        coords = new((int)precoords.X, (int)precoords.Y);

        return coords / 32;
    }
}