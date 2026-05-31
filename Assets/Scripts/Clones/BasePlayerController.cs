using Godot;
using System;
using System.Threading.Tasks;

public abstract partial class BasePlayerController : CharacterBody2D
{
    // === MOVEMENT TUNING ===
    [Export] public float AscendMultiplier = 1.5f;
    [Export] public float MoveSpeed = 250f;
    [Export] public float JumpVelocity = 350f;
    [Export] public float FallMultiplier = 2.25f;
    [Export] public float MaxFallSpeed = 800f;

    // === EFFECTS ===
    private readonly PackedScene _jumpEffectScene = GD.Load<PackedScene>("res://Assets/Scenes/Effects/jump_effect.tscn");
    private readonly PackedScene _landEffectScene = GD.Load<PackedScene>("res://Assets/Scenes/Effects/land_effect.tscn");

    // === NODES ===
    public AnimatedSprite2D sprite;
    protected AnimationPlayer animationPlayer;
    protected AnimationTree animationTree;

    // === ANIMATION PARAMS ===
    private readonly string[] _animationParams = { "idle", "isMoving", "isFalling", "jump", "kill", "levelCompleted" };

    // === INDICATOR OBJECTS
    [Export] public float offset;
    private AnimatedSprite2D indicator;

    // === STATE ===
    [Export] public bool shouldFlip;
    private bool _isLanded = false;
    [Export] public bool isFalling;
    [Export] public bool hasKey = false;

    // === JUMP BUFFERING ===
    [Export] public float JumpBufferTime = 0.15f;
    private float _jumpBufferTimer = 0f;

    // === SIGNAL BUS ===
    public static event Action MainPlayerKilled;


    public override void _Ready()
    {
        ZIndex = 10;

        sprite = GetNodeOrNull<AnimatedSprite2D>("sprite");
        animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        animationTree = GetNodeOrNull<AnimationTree>("AnimationTree");

        if (animationPlayer != null)
        {
            animationPlayer.AnimationFinished += HandleAnimationFinished;
        }
        indicator = GetNode<AnimatedSprite2D>("indicator");

        _ = WireSignalsAsync();

        if (this is not Gorgonzola)
        {
            MainPlayerKilled += OnMainPlayerKilled;
        }
    }

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

    public override void _PhysicsProcess(double delta)
    {
        isFalling = Velocity.Y >= 0f;
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

    // === Jump Logic ===
    private void HandleJump(ref Vector2 velocity, float dt)
    {
        // --- JUMP BUFFER ---
        if (Input.IsActionJustPressed("jump"))
        {
            _jumpBufferTimer = JumpBufferTime;
        }
        else
        {
            _jumpBufferTimer -= dt;
        }

        // --- START JUMP ---
        if (_jumpBufferTimer > 0f && IsOnFloor())
        {
            _jumpBufferTimer = 0f;

            velocity.Y = Math.Min(velocity.Y, -JumpVelocity);

            SpawnEffect(_jumpEffectScene, GlobalPosition);
        }
    }

    private void ApplyGravity(ref Vector2 velocity, float dt)
    {
        if (IsOnFloor())
        {
            if (!_isLanded)
            {
                SpawnEffect(_landEffectScene, GlobalPosition);
                _isLanded = true;
            }
            return;
        }

        _isLanded = false;

        float gravity = Math.Abs(GetGravity().Y);

        // --- NORMAL ASCENT ---
        if (velocity.Y < 0f) // This condition now handles all upward movement
        {
            velocity.Y += gravity * AscendMultiplier * dt;
        }
        // --- FALLING ---
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

    // === Horizontal Movement ===
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

    // === Animation ===
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
            PlayAnimation("levelCompleted");
            Gorgonzola.GetInstance().doorMoveTriggered = true;
        }
    }

    protected void PlayAnimation(string activeParam)
    {
        if (animationTree == null)
        {
            return;
        }

        foreach (string param in _animationParams)
        {
            animationTree.Set($"parameters/conditions/{param}", param == activeParam);
        }
    }

    // === Effects ===
    protected void SpawnEffect(PackedScene scene, Vector2 position)
    {
        if (scene == null) return;

        var effect = scene.Instantiate<Node2D>();
        if (effect == null) return;

        GetTree().Root.AddChild(effect);
        effect.GlobalPosition = position;

        var anim = effect.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (anim != null)
        {
            anim.FlipH = shouldFlip;
        }
    }

    // === Utility & Cleanup ===
    private void HandleAnimationFinished(StringName animName) => OnAnimationFinished(animName);

    protected virtual void OnAnimationFinished(StringName animName) { }
    protected abstract float GetMovementInput();
    public virtual bool ShouldFlipSprite(float direction) => direction < 0f;
    public virtual void Kill()
    {
        if (hasKey)
        {
            hasKey = false;
            Door.GetInstance()?.Close();
        }
    }

    protected virtual void Flush()
    {
        _isLanded = false;
        _jumpBufferTimer = 0f;
        QueueFree();
    }

    private void OnMainPlayerKilled() => Kill();
    public static void BroadcastMainPlayerKilled() => MainPlayerKilled?.Invoke();
    public static void KillAllClones() => BroadcastMainPlayerKilled();

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
            animationPlayer.AnimationFinished -= HandleAnimationFinished;
        }

        base._ExitTree();
    }

    public void SetIndicatorVisibility(bool visible)
    {
        indicator.Visible = visible;
    }

    public void ChangeIndicator(string param)
    {
        indicator.Play(param);
    }
}