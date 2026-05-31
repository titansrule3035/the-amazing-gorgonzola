using Godot;

public partial class NormClone : BasePlayerController
{
    private PackedScene killEffectScene = GD.Load<PackedScene>("res://Assets/Scenes/Effects/norm_kill_effect.tscn");
    protected override float GetMovementInput()
    {
        return Input.GetAxis("move_left", "move_right");
    }
    public override bool ShouldFlipSprite(float direction)
    {
        shouldFlip = (direction < 0);
        return shouldFlip;
    }
    public override void Kill()
    {
        SpawnEffect(killEffectScene, GlobalPosition);
        QueueFree();

        base.Kill();
    }
}
