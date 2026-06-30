using Godot;
using GodotPlugins.Game;
using System;

public partial class Spike : Sprite2D
{
    public override void _Ready()
    {
        TileMapLayer tileMapForeground = GetTree().CurrentScene.GetNode<TileMapLayer>("level/tiles/Foreground");

        Vector2 local = tileMapForeground.ToLocal(GlobalPosition);

        Vector2I cell = tileMapForeground.LocalToMap(local);

        Vector2 snappedWorld = tileMapForeground.ToGlobal(tileMapForeground.MapToLocal(cell));

        Vector2I atlasCoords = GetAtlasCoords();

        tileMapForeground.SetCell(cell, 1, atlasCoords);

        Visible = false;
    }

    public Vector2I GetAtlasCoords()
    {
        Vector2I coords = Vector2I.Zero;

        Vector2 precoords = (Texture as AtlasTexture).Region.Position;

        coords = new((int)precoords.X, (int)precoords.Y);

        return coords / 32;
    }
}