using Godot;

public partial class DefaultClone : BasePlayerController
{
    // Scenes / resources
    [Export] private PackedScene killEffectScene;

    /// <summary>
    /// Returns horizontal input for the default clone using the configured input actions.
    /// </summary>
    /// <returns>Normalized horizontal input (-1..1).</returns>
    protected override float GetMovementInput()
    {
        return Input.GetAxis("move_left", "move_right");
    }

    /// <summary>
    /// Flips the sprite based on movement direction and stores the flip state.
    /// </summary>
    /// <param name="direction">Horizontal movement direction.</param>
    /// <returns>True if sprite should be flipped horizontally.</returns>
    public override bool ShouldFlipSprite(float direction)
    {
        shouldFlip = (direction < 0);
        return shouldFlip;
    }

    /// <summary>
    /// Handles death for the default clone: spawns a kill effect, queues the node for freeing,
    /// and invokes base-class kill logic (key handling, UI updates).
    /// </summary>
    public override void Kill()
    {
        SpawnEffect(killEffectScene, GlobalPosition);
        QueueFree();

        base.Kill();
    }
}
