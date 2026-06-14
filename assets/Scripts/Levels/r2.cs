using Godot;
using System;

public partial class r2 : LocalGameManager
{
    public override void _Ready()
    {
        MoveToTitle();

        base._Ready();
    }

    public async void MoveToTitle()
    {
        await ToSignal(GetTree().CreateTimer(5.0f), SceneTreeTimer.SignalName.Timeout);
        GlobalGameManager.GetInstance().levelCompleted = true;
        CanvasEffects.GetInstance().FadeOut(Colors.Black);
    }
}
