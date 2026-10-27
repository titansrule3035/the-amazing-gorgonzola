using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public partial class EditorTileObject : EditorItemObject
{
    [Export] public int tileID = 0;

    [Export] public PackedScene collider;
    public override void _Ready()
    {
        base._Ready();

        itemType = ItemType.Tile;

        Texture = GetTileTexture();
    }

    public Texture2D GetTileTexture()
    {
        return Texture;
    }

    public Vector2I GetAtlasCoords()
    {
        Vector2I coords = Vector2I.Zero;

        Vector2 precoords = (Texture as AtlasTexture).Region.Position;

        coords = new((int)precoords.X, (int)precoords.Y);

        return coords / 32;
    }

    public Rect2 GetRegion()
    {
        return (Texture as AtlasTexture).Region;
    }

    public override void UpdateTextures(Texture2D texture, bool flipH, bool selected)
    {
        Sprite2D objectCursor = GetTree().CurrentScene.GetNode<Sprite2D>("editor/main/EditorObject/Sprite");

        // Cursor ghost: visible while a tile is selected, hidden when cleared.
        Godot.Color modulate = objectCursor.Modulate;
        objectCursor.Modulate = new Godot.Color(modulate.R, modulate.G, modulate.B, selected ? 0.5f : 0f);

        if (selected)
        {
            SendItemToEditor();
            Editor.Set("current_tile", Texture);
            ChangeTexture?.Invoke(this);
        }
        else
        {
            // Resets every palette item to "default" and calls Editor.ClearEditorItem() (see SetActiveItem).
            ChangeTexture?.Invoke(null);
        }
    }

    public override void SendItemToEditor()
    {
        base.SendItemToEditor();
    }
}