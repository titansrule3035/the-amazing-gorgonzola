using Godot;

public partial class NormClone : BasePlayerController
{
    [Export] private PackedScene killEffectScene;
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
