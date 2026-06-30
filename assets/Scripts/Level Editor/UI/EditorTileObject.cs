using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAGLevelBuilder.assets.Scripts.Level_Editor
{
    public partial class EditorTileObject : EditorItemObject
    {
        [Export] public int tileID = 0;

        [Export] public PackedScene collider;
        public override void _Ready()
        {
            base._Ready();

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

            coords = new((int) precoords.X, (int) precoords.Y);

            return coords / 32;
        }

        public Rect2 GetRegion()
        {
            return (Texture as AtlasTexture).Region;
        }

        public override void UpdateTextures(Texture2D texture, bool flipH, bool selected)
        {
            Sprite2D objectCursor = GetTree().CurrentScene.GetNode<Sprite2D>("EditorObject/Sprite");
            EditorObject editorObject = GetTree().CurrentScene.GetNode<EditorObject>("EditorObject");

            editorObject.SetEditorItem(this);

            editorObject.Set("current_tile", Texture);

            float alpha = 0f;
            if (selected)
            {
                alpha = 0.5f;
            }
            Godot.Color modulate = objectCursor.Modulate;
            modulate = new(modulate.R, modulate.G, modulate.B, alpha);

            objectCursor.Modulate = modulate;

            ChangeTexture.Invoke(this);
        }

        public override void SendItemToEditor()
        {
            base.SendItemToEditor();
        }
    }
}
