using Godot;
using System;
using System.Threading.Tasks;

public abstract partial class BasePlayerController : CharacterBody2D
{
    // Exported tunables
    [Export] public bool autoAnimate = true;
    [Export] public float AscendMultiplier = 1.5f;
    [Export] public float MoveSpeed = 250f;
    [Export] public float JumpVelocity = 350f;
    [Export] public float FallMultiplier = 2.25f;
    [Export] public float MaxFallSpeed = 800f;
    [Export] public float offset;
    [Export] public bool shouldFlip;
    [Export] public bool isFalling;
    [Export] public bool hasKey = false;
    [Export] public float JumpBufferTime = 0.15f;

    /// <summary>
    /// When false, this controller ignores player input entirely (no horizontal movement, no jumping).
    /// Gravity, physics and animations still run. Use SetPlayerInputEnabled() to change it at runtime.
    /// </summary>
    [Export] public bool AllowPlayerInput = true;

    // Scenes / resources
    [Export] private PackedScene jumpEffectScene;
    [Export] private PackedScene landEffectScene;

    // Node references
    public AnimatedSprite2D sprite;
    private AnimatedSprite2D indicator;
    protected AnimationPlayer animationPlayer;
    protected AnimationTree animationTree;

    // Animation settings
    private readonly string[] animationParams = { "idle", "isMoving", "isFalling", "jump", "kill", "levelCompleted", "enter_door" };

    // Internal state
    private bool isLanded = false;
    private float jumpBufferTimer = 0f;
    public bool killed = false;

    // Events
    public static event Action MainPlayerKilled;

    // ------------------------------------------------------------------
    // Game state helpers (GlobalGameManager in-game, EditorGameManager in editor)
    // ------------------------------------------------------------------

    /// <summary>True when the active game manager reports the level as completed.</summary>
    private static bool IsLevelCompleted()
    {
        GlobalGameManager? ggm = GlobalGameManager.GetInstance();
        if (ggm != null)
        {
            return ggm.levelCompleted;
        }

        EditorGameManager? egm = EditorGameManager.GetInstance();
        return egm != null && egm.levelCompleted;
    }

    /// <summary>True when the active game manager reports the game as paused.</summary>
    private static bool IsGamePaused()
    {
        GlobalGameManager? ggm = GlobalGameManager.GetInstance();
        if (ggm != null)
        {
            return ggm.gamePaused;
        }

        EditorGameManager? egm = EditorGameManager.GetInstance();
        return egm != null && egm.gamePaused;
    }

    /// <summary>True when the active game manager allows movement.</summary>
    private static bool CanMoveNow()
    {
        GlobalGameManager? ggm = GlobalGameManager.GetInstance();
        if (ggm != null)
        {
            return ggm.canMove;
        }

        EditorGameManager? egm = EditorGameManager.GetInstance();
        return egm == null || egm.canMove;
    }

    /// <summary>
    /// Called when the node is added to the scene. Initializes node references, wires signals,
    /// and sets up animation and event subscriptions.
    /// </summary>
    public override void _Ready()
    {
        ZIndex = 10;

        sprite = GetNodeOrNull<AnimatedSprite2D>("sprite");
        animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        animationTree = GetNodeOrNull<AnimationTree>("AnimationTree");

        if (animationPlayer != null)
        {
            animationTree.AnimationFinished += OnAnimationFinished;
        }
        indicator = GetNode<AnimatedSprite2D>("indicator");

        _ = WireSignalsAsync();

        if (this is not Gorgonzola)
        {
            MainPlayerKilled += OnMainPlayerKilled;
        }

        animationTree.Active = true;
    }

    /// <summary>
    /// Physics update loop. Handles input, movement, gravity application, and animation updates.
    /// </summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector2 velocity = Velocity;

        if (!IsLevelCompleted() && !IsGamePaused())
        {
            // No input is read when the flag is off; direction stays 0 so the character stops horizontally.
            float direction = AllowPlayerInput ? GetMovementInput() : 0f;
            HandleHorizontalMovement(ref velocity, direction);
            HandleJump(ref velocity, dt);
        }
        else
        {
            velocity = new Vector2(0, velocity.Y);
        }

        ApplyGravity(ref velocity, dt);

        Velocity = velocity;
        MoveAndSlide();

        // Ceiling fix: when the head hits a ceiling, MoveAndSlide can leave residual upward
        // velocity (especially when also pressed into a wall / corner). Kill it so gravity
        // starts pulling the character down on the very next frame.
        if (IsOnCeiling() && Velocity.Y < 0f)
        {
            Velocity = new Vector2(Velocity.X, 0f);
        }

        // Use the post-collision velocity so animations reflect what actually happened.
        UpdateAnimation(Velocity);

        indicator.GlobalPosition = new Vector2(GlobalPosition.X, GlobalPosition.Y - offset);
    }

    /// <summary>
    /// Cleans up event subscriptions when the node exits the scene tree.
    /// </summary>
    public override void _ExitTree()
    {
        var gm = LocalGameManager.GetInstance();
        if (gm != null)
        {
            gm.OnFlush -= Flush;
        }

        if (this is not Gorgonzola)
        {
            MainPlayerKilled -= OnMainPlayerKilled;
        }

        if (animationPlayer != null)
        {
            animationTree.AnimationFinished -= OnAnimationFinished;
        }

        base._ExitTree();
    }

    /// <summary>
    /// Waits until the LocalGameManager instance is available and then wires the Flush event.
    /// This runs asynchronously so the node can finish initializing without blocking.
    /// </summary>
    private async Task WireSignalsAsync()
    {
        while (LocalGameManager.GetInstance() == null && IsInsideTree())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var gm = LocalGameManager.GetInstance();

        if (gm != null)
        {
            gm.OnFlush += Flush;
        }
    }

    /// <summary>
    /// Enables or disables player input for this controller. Disabling clears any buffered jump
    /// so the character doesn't jump the moment input is re-enabled.
    /// </summary>
    /// <param name="allowed">True to let player input move this controller.</param>
    public void SetPlayerInputEnabled(bool allowed)
    {
        AllowPlayerInput = allowed;

        if (!allowed)
        {
            jumpBufferTimer = 0f;
        }
    }


    /// <summary>
    /// Processes jump input including buffering and spawns the jump effect when a jump occurs.
    /// Does nothing (and clears the buffer) while player input is disabled.
    /// </summary>
    /// <param name="velocity">Character velocity, passed by reference to be modified.</param>
    /// <param name="dt">Delta time in seconds.</param>
    private void HandleJump(ref Vector2 velocity, float dt)
    {
        if (!AllowPlayerInput)
        {
            jumpBufferTimer = 0f;
            return;
        }

        if (IsJumpJustPressed() && CanMoveNow())
        {
            jumpBufferTimer = JumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= dt;
        }

        if (jumpBufferTimer > 0f && IsOnFloor())
        {
            jumpBufferTimer = 0f;

            velocity.Y = Math.Min(velocity.Y, -JumpVelocity);

            SpawnEffect(jumpEffectScene, GlobalPosition);
        }
    }

    /// <summary>
    /// Makes this controller jump immediately, ignoring input, the AllowPlayerInput flag,
    /// the jump buffer and whether it is on the ground.
    /// </summary>
    /// <param name="strength">Optional jump velocity override. Defaults to JumpVelocity.</param>
    /// <param name="spawnEffect">Whether to spawn the jump effect.</param>
    public void ForceJump(float? strength = null, bool spawnEffect = true)
    {
        float jumpSpeed = strength ?? JumpVelocity;

        Velocity = new Vector2(Velocity.X, -jumpSpeed);
        jumpBufferTimer = 0f;

        if (spawnEffect)
        {
            SpawnEffect(jumpEffectScene, GlobalPosition);
        }
    }

    /// <summary>
    /// Applies gravity to the character taking into account ascent and fall multipliers
    /// and clamps the fall speed to MaxFallSpeed. Plays landing effect when touching the floor.
    /// </summary>
    /// <param name="velocity">Character velocity, passed by reference to be modified.</param>
    /// <param name="dt">Delta time in seconds.</param>
    private void ApplyGravity(ref Vector2 velocity, float dt)
    {
        isFalling = velocity.Y >= 0f;

        if (IsOnFloor())
        {
            if (!isLanded)
            {
                SpawnEffect(landEffectScene, GlobalPosition);
                isLanded = true;
            }
            return;
        }

        isLanded = false;

        // Bonked the ceiling: cancel any remaining upward speed immediately.
        if (IsOnCeiling() && velocity.Y < 0f)
        {
            velocity.Y = 0f;
        }

        float gravity = Math.Abs(GetGravity().Y);

        // Ascend multiplier only while genuinely rising; otherwise (including Y == 0) use fall gravity.
        float multiplier = velocity.Y < 0f ? AscendMultiplier : FallMultiplier;
        velocity.Y += gravity * multiplier * dt;

        if (velocity.Y > MaxFallSpeed)
        {
            velocity.Y = MaxFallSpeed;
        }
    }

    /// <summary>
    /// Handles horizontal movement input and flipping the sprite if necessary.
    /// </summary>
    /// <param name="velocity">Character velocity, passed by reference to be modified.</param>
    /// <param name="direction">Normalized horizontal input direction (-1..1).</param>
    private void HandleHorizontalMovement(ref Vector2 velocity, float direction)
    {
        if (IsLevelCompleted() || !CanMoveNow())
        {
            velocity.X = 0f;
            return;
        }

        float accel = MoveSpeed;

        if (direction != 0f)
        {
            velocity.X = direction * accel;
            if (sprite != null)
            {
                sprite.FlipH = ShouldFlipSprite(direction);
            }
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0f, accel);
        }
    }

    /// <summary>
    /// Updates the animation state machine based on movement and game state.
    /// </summary>
    /// <param name="velocity">Current character velocity.</param>
    private void UpdateAnimation(Vector2 velocity)
    {
        if (animationTree == null || !autoAnimate)
        {
            return;
        }

        bool canMove = CanMoveNow();

        if (!IsLevelCompleted() && canMove)
        {
            if (IsOnFloor())
            {
                PlayAnimation(Mathf.Abs(velocity.X) > 1f ? "isMoving" : "idle");
            }
            else
            {
                PlayAnimation(velocity.Y > 0f ? "isFalling" : "jump");
            }
        }
        else if (canMove)
        {
            if (this is Gorgonzola gorgonzola)
            {
                PlayAnimation("enter_door");
                gorgonzola.doorMoveTriggered = true;
            }
            else if (this is ShadowClone shadowClone)
            {
                shadowClone.Kill();
            }
            else
            {
                PlayAnimation("levelCompleted");
            }
        }
    }

    /// <summary>
    /// Activates the named animation parameter while deactivating others.
    /// </summary>
    /// <param name="activeParam">The animation parameter to set active.</param>
    protected void PlayAnimation(string activeParam)
    {
        if (animationTree == null)
        {
            return;
        }

        foreach (string param in animationParams)
        {
            animationTree.Set($"parameters/conditions/{param}", param == activeParam);
        }
    }

    /// <summary>
    /// Instantiates a particle/visual effect scene at the given world position and adds it to the tree.
    /// </summary>
    /// <param name="scene">PackedScene for the effect.</param>
    /// <param name="position">Global position to place the effect.</param>
    protected void SpawnEffect(PackedScene scene, Vector2 position)
    {
        if (scene == null) return;

        var effect = scene.Instantiate<Effect>();
        if (effect == null) return;

        GetParent<Node2D>().AddChild(effect);

        effect.GlobalPosition = position;

        var anim = effect.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (anim != null)
        {
            anim.FlipH = shouldFlip;
        }
    }

    /// <summary>
    /// Called when an animation on the AnimationPlayer finishes. Handles transitions for
    /// enter door and level completed animations.
    /// </summary>
    /// <param name="animName">Name of the finished animation.</param>
    private void OnAnimationFinished(StringName animName)
    {
        if (animName == "enter_door")
        {
            GlobalGameManager? ggm = GlobalGameManager.GetInstance();
            if (ggm != null)
            {
                if (ggm.levelCompleted)
                {
                    Visible = false;
                }
                ggm.canMove = false;
            }
        }
        else if (animName == "levelCompleted")
        {
            Visible = false;
        }
    }

    /// <summary>
    /// Implement in derived classes to provide a horizontal movement input value (-1..1).
    /// Only called while AllowPlayerInput is true.
    /// </summary>
    /// <returns>Horizontal input direction.</returns>
    protected abstract float GetMovementInput();

    /// <summary>
    /// Determines whether the sprite should be flipped based on movement direction.
    /// </summary>
    /// <param name="direction">Horizontal direction value.</param>
    /// <returns>True if sprite should be flipped horizontally.</returns>
    public virtual bool ShouldFlipSprite(float direction) => direction < 0f;

    /// <summary>
    /// Whether a jump was requested this physics frame. Override to change where jump input comes from.
    /// </summary>
    protected virtual bool IsJumpJustPressed() => Input.IsActionJustPressed("jump");

    /// <summary>
    /// Handles logic when this player/controller is killed. Releases keys and updates UI.
    /// </summary>
    public virtual void Kill()
    {
        if (hasKey)
        {
            hasKey = false;
            Door.GetInstance()?.Close();
            Key.GetInstance().BlackenUIElement();
        }
    }

    /// <summary>
    /// Called on game flush/reset. Resets transient state and queues the node for freeing.
    /// </summary>
    protected virtual void Flush()
    {
        isLanded = false;
        jumpBufferTimer = 0f;
        if (this is not Gorgonzola)
        {
            QueueFree();
        }
    }

    /// <summary>
    /// Local handler that responds to the static MainPlayerKilled event by calling Kill().
    /// </summary>
    protected void OnMainPlayerKilled() => Kill();

    /// <summary>
    /// Broadcasts the static MainPlayerKilled event to notify all listeners.
    /// </summary>
    public static void BroadcastMainPlayerKilled() => MainPlayerKilled?.Invoke();

    /// <summary>
    /// Convenience helper to broadcast that all clones should be killed.
    /// </summary>
    public static void KillAllClones() => BroadcastMainPlayerKilled();


    /// <summary>
    /// Shows or hides the indicator AnimatedSprite2D.
    /// </summary>
    /// <param name="visible">Whether the indicator should be visible.</param>
    public void SetIndicatorVisibility(bool visible)
    {
        indicator.Visible = visible;
    }

    /// <summary>
    /// Plays the given animation on the indicator sprite.
    /// </summary>
    /// <param name="param">Animation name to play on the indicator.</param>
    public void ChangeIndicator(string param)
    {
        indicator.Play(param);
    }
}