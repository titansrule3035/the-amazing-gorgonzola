using Godot;
using System;

public partial class r2 : LocalGameManager
{
    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Input.IsActionJustPressed("jump"))
        {
            Color col = new Color(0, 0, 0, 1);
            CanvasEffects.GetInstance().FadeOut(col);
        }
    }
}
