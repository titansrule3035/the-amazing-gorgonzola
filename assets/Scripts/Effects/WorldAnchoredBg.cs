using Godot;

public partial class WorldAnchoredBg : ColorRect
{
    [Export] public Texture2D bgTexture;
    [Export] public Vector2 textureScale = Vector2.One;

    [Export(PropertyHint.Range, "0,1,0.01,or_greater,or_less")]
    float parallaxFactor = 1.0f;
    [Export] public Vector2 scrollSpeed = Vector2.Zero;

    private ShaderMaterial shaderMaterial;
    private Vector2 scrollOffset;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shaderMaterial = Material as ShaderMaterial;
    }

    public override void _Process(double delta)
    {
        if (shaderMaterial == null || bgTexture == null)
        {
            return;
        }

        Vector2 scale = new Vector2(Mathf.Max(Mathf.Abs(textureScale.X), 0.0001f), Mathf.Max(Mathf.Abs(textureScale.Y), 0.0001f));
        Vector2 period = bgTexture.GetSize() / scale;

        scrollOffset += scrollSpeed * (float)delta;
        scrollOffset = new Vector2(Mathf.PosMod(scrollOffset.X, period.X), Mathf.PosMod(scrollOffset.Y, period.Y));


        Transform2D inverse = GetViewport().GetCanvasTransform().AffineInverse();

        shaderMaterial.SetShaderParameter("background_texture", bgTexture);
        shaderMaterial.SetShaderParameter("texture_scale", textureScale);
        shaderMaterial.SetShaderParameter("parallax_factor", parallaxFactor);
        shaderMaterial.SetShaderParameter("scroll_offset", scrollOffset);
        shaderMaterial.SetShaderParameter("viewport_size", GetViewportRect().Size);
        shaderMaterial.SetShaderParameter("world_from_screen_x", inverse.X);
        shaderMaterial.SetShaderParameter("world_from_screen_y", inverse.Y);
        shaderMaterial.SetShaderParameter("world_from_screen_origin", inverse.Origin);
    }
}
