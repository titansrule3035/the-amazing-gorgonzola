using Godot;
using System;
using System.Threading.Tasks;

public abstract partial class BasePlayerController : CharacterBody2D
{
    // Exported tunables
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

    // Events
    public static event Action MainPlayerKilled;

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
    /// Physics update loop. Handles input, movement, gravity application, and animation updates.
    /// </summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector2 velocity = Velocity;

        float direction = 0f;
        if (!GlobalGameManager.GetInstance().levelCompleted && !GlobalGameManager.GetInstance().gamePaused)
        {
            direction = GetMovementInput();
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

        UpdateAnimation(velocity);

        indicator.GlobalPosition = new Vector2(GlobalPosition.X, GlobalPosition.Y - offset);
    }

    /// <summary>
    /// Processes jump input including buffering and spawns the jump effect when a jump occurs.
    /// </summary>
    /// <param name="velocity">Character velocity, passed by reference to be modified.</param>
    /// <param name="dt">Delta time in seconds.</param>
    private void HandleJump(ref Vector2 velocity, float dt)
    {
        if (Input.IsActionJustPressed("jump") && GlobalGameManager.GetInstance().canMove)
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
    /// Applies gravity to the character taking into account ascent and fall multipliers
    /// and clamps the fall speed to MaxFallSpeed. Plays landing effect when touching the floor.
    /// </summary>
    /// <param name="velocity">Character velocity, passed by reference to be modified.</param>
    /// <param name="dt">Delta time in seconds.</param>
    private void ApplyGravity(ref Vector2 velocity, float dt)
    {
        isFalling = Velocity.Y >= 0f;
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

        float gravity = Math.Abs(GetGravity().Y);

        if (velocity.Y < 0f)
        {
            velocity.Y += gravity * AscendMultiplier * dt;
        }
        else if (velocity.Y > 0f)
        {
            velocity.Y += gravity * FallMultiplier * dt;
        }
        else
        {
            velocity.Y += gravity * dt;
        }

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
        if (!GlobalGameManager.GetInstance().levelCompleted && GlobalGameManager.GetInstance().canMove)
        {
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
        else
        {
            velocity.X = 0;
        }
    }

    /// <summary>
    /// Updates the animation state machine based on movement and game state.
    /// </summary>
    /// <param name="velocity">Current character velocity.</param>
    private void UpdateAnimation(Vector2 velocity)
    {
        if (animationTree == null)
        {
            return;
        }

        if (!GlobalGameManager.GetInstance().levelCompleted && GlobalGameManager.GetInstance().canMove)
        {
            if (IsOnFloor())
            {
                if (Mathf.Abs(velocity.X) > 1f)
                {
                    PlayAnimation("isMoving");
                }
                else
                {
                    PlayAnimation("idle");
                }
            }
            else
            {
                if (velocity.Y > 0f)
                {
                    PlayAnimation("isFalling");
                }
                else
                {
                    PlayAnimation("jump");
                }
            }
        }
        else if (GlobalGameManager.GetInstance().canMove)
        {
            if (this is Gorgonzola)
            {
                PlayAnimation("enter_door");
            }
            else
            {
                PlayAnimation("levelCompleted");
            }
            Gorgonzola.GetInstance().doorMoveTriggered = true;
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

        var effect = scene.Instantiate<Node2D>();
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
            GlobalGameManager ggm = GlobalGameManager.GetInstance();
            if (ggm.levelCompleted)
            {
                Visible = false;
            }
            ggm.canMove = false;
        }
        else if (animName == "levelCompleted")
        {
            Visible = false;
        }
    }
    /// <summary>
    /// Implement in derived classes to provide a horizontal movement input value (-1..1).
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
    private void OnMainPlayerKilled() => Kill();
    /// <summary>
    /// Broadcasts the static MainPlayerKilled event to notify all listeners.
    /// </summary>
    public static void BroadcastMainPlayerKilled() => MainPlayerKilled?.Invoke();
    /// <summary>
    /// Convenience helper to broadcast that all clones should be killed.
    /// </summary>
    public static void KillAllClones() => BroadcastMainPlayerKilled();

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