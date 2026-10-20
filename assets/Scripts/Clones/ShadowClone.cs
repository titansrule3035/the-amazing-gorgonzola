using Godot;
using System.Collections.Generic;

public partial class ShadowClone : BasePlayerController
{
    // Scenes / resources
    [Export] private PackedScene formEffectScene;
    [Export] private PackedScene killEffectScene;
    [Export] private int delayMs = 1500;

    private struct InputSample
    {
        public float Time;
        public float Direction;
        public bool Jump;
    }

    private readonly Queue<InputSample> inputQueue = new();
    private float elapsed = 0f;

    // The delayed values the base class reads this frame
    private float delayedDirection = 0f;
    private bool delayedJump = false;

    public override void _PhysicsProcess(double delta)
    {
        elapsed += (float)delta;

        // 1. Record the live input (same sources as DefaultClone)
        inputQueue.Enqueue(new InputSample
        {
            Time = elapsed,
            Direction = Input.GetAxis("move_left", "move_right"),
            Jump = Input.IsActionJustPressed("jump")
        });

        // 2. Release every sample that is now at least `delayMs` old
        float delaySeconds = delayMs / 1000f;
        delayedJump = false;

        while (inputQueue.Count > 0 && inputQueue.Peek().Time <= elapsed - delaySeconds)
        {
            InputSample sample = inputQueue.Dequeue();
            delayedDirection = sample.Direction;
            delayedJump |= sample.Jump; // a one-frame press must not be lost
        }

        // 3. Run normal movement/gravity/animation using the delayed input
        base._PhysicsProcess(delta);
    }

    protected override float GetMovementInput() => delayedDirection;

    protected override bool IsJumpJustPressed() => delayedJump;

    public override bool ShouldFlipSprite(float direction)
    {
        shouldFlip = (direction < 0);
        return shouldFlip;
    }

    public override void Kill()
    {
        if (!killed)
        {
            killed = true;
            SpawnEffect(killEffectScene, GlobalPosition);
            QueueFree();

            base.Kill();
        }
    }

    protected override void Flush()
    {
        inputQueue.Clear();
        delayedDirection = 0f;
        delayedJump = false;
        base.Flush();
    }
}