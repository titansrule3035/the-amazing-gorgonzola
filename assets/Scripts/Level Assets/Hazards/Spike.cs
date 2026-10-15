using Godot;
using GodotPlugins.Game;
using System;
using System.Threading.Tasks;

public partial class Spike : Node2D
{
    public override async void _Ready()
    {
        // Let the spawner finish setting Position before we read GlobalPosition
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (!IsInsideTree()) return;

        Node levelRoot = GetTree().CurrentScene.GetNode<Node2D>(
            ((Runtime)GetTree().CurrentScene).editorMode ? "editor/main" : "game/main");

        TileMapLayer tileMapForeground = levelRoot.GetNode<TileMapLayer>("level/tiles/Foreground");

        Vector2 local = tileMapForeground.ToLocal(GlobalPosition);
        Vector2I cell = tileMapForeground.LocalToMap(local);

        Vector2I atlasCoords = GetAtlasCoords();

        tileMapForeground.SetCell(new Vector2I(cell.X, cell.Y - 1), 1, atlasCoords);

        Visible = false;
    }

    public Vector2I GetAtlasCoords()
    {
        Vector2 precoords = (GetNode<Sprite2D>("sprite").Texture as AtlasTexture).Region.Position;
        return new Vector2I((int)precoords.X, (int)precoords.Y) / 32;
    }
}