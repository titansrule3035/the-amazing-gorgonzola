using Godot;
using System;

public partial class Filter : ColorRect
{
    // Singleton
    private static Filter instance;

    // Lifecycle
    public override void _Ready()
    {
        instance = this;
        SetPanelColor(new(0, 0, 0, 0));
    }

    // Helper
    public void SetPanelColor(Godot.Color color)
    {
        this.Color = color;
        Modulate = new(Colors.White.R, Colors.White.G, Colors.White.B, color.A);
    }

    public static Filter GetInstance()
    {
        return instance;
    }
}
