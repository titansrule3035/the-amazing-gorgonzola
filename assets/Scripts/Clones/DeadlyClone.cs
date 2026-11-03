using Godot;

public partial class DeadlyClone : BasePlayerController
{
    // Scenes / resources
    [Export] private PackedScene killEffectScene;
    private Area2D localKillZone;

    public override void _Ready()
    {
        base._Ready();

        localKillZone = GetNode<Area2D>("area_2d");
        localKillZone.BodyEntered += OnBodyEntered;
    }

    void OnBodyEntered(Node body)
    {
        if (body == this)
        {
            return;
        }

        if (body is BasePlayerController player)
        {
            if (player is DeadlyClone || player is ShadowClone)
            {
                return;
            }

            player.CallDeferred("Kill");
        }
    }

    /// <summary>
    /// Returns horizontal input for the clone using the configured input actions.
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
    /// Handles death for the clone: spawns a kill effect, queues the node for freeing,
    /// and invokes base-class kill logic (key handling, UI updates).
    /// </summary>
    public override void Kill()
    {
        if (!killed)
        {
            killed = true;
            Effect.SpawnEffect(this, killEffectScene, GlobalPosition, shouldFlip);
            QueueFree();

            base.Kill();
        }
    }
}
