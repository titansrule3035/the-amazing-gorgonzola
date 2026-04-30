using Godot;

public partial class RevClone : BasePlayerController
{
    private PackedScene killEffectScene = GD.Load<PackedScene>("res://Assets/Scenes/Effects/rev_kill_effect.tscn");
    protected override float GetMovementInput()
    {
        return Input.GetAxis("move_right", "move_left"); // Reversed
    }
    public override bool ShouldFlipSprite(float direction)
    {
        shouldFlip = !(direction < 0);
        return shouldFlip;
    }
    public override void Kill()
    {
        SpawnEffect(killEffectScene, GlobalPosition);
        QueueFree();
    }
}
