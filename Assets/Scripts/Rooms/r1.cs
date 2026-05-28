using Godot;
using System;

public partial class r1 : LocalGameManager
{
    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Input.IsActionJustPressed("jump"))
        {
            Color col = new Color(0, 0, 0, 1);
            GlobalGameManager.GetInstance().levelCompleted = true;
            CanvasEffects.GetInstance().FadeOut(col);
        }
    }
}
