using Godot;
using System.Collections.Generic;

public partial class ShadowClone : BasePlayerController
{
    // Scenes / resources
    [Export] private PackedScene killEffectScene;

    // Mode switch:
    //   false = replay the player's own input, delayed by delayMs (original behavior)
    //   true  = chase where Gorgonzola was ChaseDelayMs ago
    [Export] public bool UseChaseMode = false;

    // --- Input replay settings (original) ---
    [Export] private new int delayMs = 1500;

    // --- Chase settings ---
    [Export] public int ChaseDelayMs = 750;          // how far behind Gorgonzola's trail this clone runs
    [Export] public float StopDistanceX = 6f;        // horizontal deadzone to avoid jitter
    [Export] public float JumpIfTargetAbove = 40f;   // jump when the delayed point is this much higher
    [Export] public float JumpCooldown = 0.3f;       // minimum time between chase jumps

    // ------------------------------------------------------------------
    // Input replay state (original)
    // ------------------------------------------------------------------
    private struct InputSample
    {
        public float Time;
        public float Direction;
        public bool Jump;
    }

    private readonly Queue<InputSample> inputQueue = new();

    // ------------------------------------------------------------------
    // Chase state
    // ------------------------------------------------------------------
    private struct PositionSample
    {
        public float Time;
        public Vector2 Position;
    }

    private readonly Queue<PositionSample> history = new();
    public BasePlayerController target;
    private float jumpCooldownTimer = 0f;
    private float findTargetTimer = 0f;
    private Vector2 delayedTargetPosition;
    private bool hasDelayedTarget = false;

    // ------------------------------------------------------------------
    // Shared state
    // ------------------------------------------------------------------
    private float elapsed = 0f;

    // The values the base class reads this frame (filled by whichever mode is active)
    private float delayedDirection = 0f;
    private bool delayedJump = false;

    public override void _PhysicsProcess(double delta)
    {
        elapsed += (float)delta;

        if (UseChaseMode)
        {
            ProcessChaseMode((float)delta);
        }
        else
        {
            ProcessInputReplayMode();
        }

        // Run normal movement/gravity/animation using the delayed/chase input
        base._PhysicsProcess(delta);
    }

    // ------------------------------------------------------------------
    // Mode 1: replay the player's input, delayed (original code)
    // ------------------------------------------------------------------
    private void ProcessInputReplayMode()
    {
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
    }

    // ------------------------------------------------------------------
    // Mode 2: chase where Gorgonzola was ChaseDelayMs ago
    // ------------------------------------------------------------------
    private void ProcessChaseMode(float dt)
    {
        jumpCooldownTimer -= dt;

        delayedDirection = 0f;
        delayedJump = false;

        FindTarget(dt);
        RecordAndReleaseTrail();

        // Nothing to chase until the first sample is ChaseDelayMs old
        if (hasDelayedTarget)
        {
            UpdateChaseInput();
        }
    }

    /// <summary>
    /// Records Gorgonzola's current position and releases every sample that is now
    /// at least ChaseDelayMs old. The newest released sample is the point to chase.
    /// </summary>
    private void RecordAndReleaseTrail()
    {
        if (target != null)
        {
            history.Enqueue(new PositionSample
            {
                Time = elapsed,
                Position = target.GlobalPosition
            });
        }

        float delaySeconds = ChaseDelayMs / 1000f;

        while (history.Count > 0 && history.Peek().Time <= elapsed - delaySeconds)
        {
            delayedTargetPosition = history.Dequeue().Position;
            hasDelayedTarget = true;
        }
    }

    /// <summary>
    /// Decides this frame's direction and jump based on the delayed trail point.
    /// </summary>
    private void UpdateChaseInput()
    {
        Vector2 toTarget = delayedTargetPosition - GlobalPosition;

        if (Mathf.Abs(toTarget.X) > StopDistanceX)
        {
            delayedDirection = Mathf.Sign(toTarget.X);
        }

        if (IsOnFloor() && jumpCooldownTimer <= 0f)
        {
            bool targetIsAbove = toTarget.Y < -JumpIfTargetAbove;
            bool blockedByWall = IsOnWall() && delayedDirection != 0f;

            if (targetIsAbove || blockedByWall)
            {
                delayedJump = true;
                jumpCooldownTimer = JumpCooldown;
            }
        }
    }

    /// <summary>
    /// Finds Gorgonzola among this clone's siblings. Retries a few times per second
    /// if the reference is missing or has been freed.
    /// </summary>
    private void FindTarget(float dt)
    {
        if (target != null && IsInstanceValid(target))
        {
            return;
        }

        target = null;
        findTargetTimer -= dt;
        if (findTargetTimer > 0f)
        {
            return;
        }
        findTargetTimer = 0.25f;

        Node parent = GetParent();
        if (parent == null)
        {
            return;
        }

        foreach (Node child in parent.GetChildren())
        {
            if (child is Gorgonzola gorgonzola)
            {
                target = gorgonzola;
                return;
            }
        }
    }

    // ------------------------------------------------------------------
    // Base class hooks
    // ------------------------------------------------------------------
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
        history.Clear();
        hasDelayedTarget = false;
        delayedDirection = 0f;
        delayedJump = false;
        base.Flush();
    }
}