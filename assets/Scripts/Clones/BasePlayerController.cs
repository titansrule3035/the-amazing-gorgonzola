using Godot;
using System;
using System.Threading.Tasks;

public partial class BasePlayerController : CharacterBody2D
{
    // clone type enum for exporting/importing in the editor
    public enum CloneType
    {
        Player,
        Normal,
        Reverse,
        UpsideDown,
        ReverseUpsideDown,
        Shadow,
        Deadly,
        Default
    }

    // Exported tunables
    [Export] public bool autoAnimate = true;
    [Export] public float AscendMultiplier = 1.5f;
    [Export] public float MoveSpeed = 250f;
    [Export] public float JumpVelocity = 350f;
    [Export] public float FallMultiplier = 2.25f;
    [Export] public float MaxFallSpeed = 800f;
    [Export] public float offset;
    [Export] public bool isFalling;
    [Export] public bool hasKey = false;
    [Export] public float JumpBufferTime = 0.15f;
    [Export] public CloneType cloneType = CloneType.Default;

    /// <summary>
    /// When false, this controller ignores player input entirely (no horizontal movement, no jumping).
    /// Gravity, physics and animations still run. Use SetPlayerInputEnabled() to change it at runtime.
    /// </summary>
    [Export] public bool AllowPlayerInput = true;

    // Scenes / resources
    [Export] private PackedScene jumpEffectScene;
    [Export] private PackedScene landEffectScene;
    [Export] private PackedScene killEffectScene;

    // Node references
    public AnimatedSprite2D sprite;
    private AnimatedSprite2D indicator;
    private CollisionShape2D bodyShape;
    protected AnimationPlayer animationPlayer;
    protected AnimationTree animationTree;

    // Animation settings
    private readonly string[] animationParams = { "idle", "isMoving", "isFalling", "jump", "kill", "levelCompleted", "enter_door" };

    // Internal state
    private bool isLanded = false;
    private bool groundStateInitialized = false;

    // Which way the character last faced, as a local FlipH flag. Tracked here instead of being read
    // back from sprite.FlipH, which can be null or overwritten by animations.
    private bool facingFlipH = false;
    private float jumpBufferTimer = 0f;
    public bool killed = false;

    // Events
    public static event Action MainPlayerKilled;

    // ------------------------------------------------------------------
    // Scale-driven orientation
    // ------------------------------------------------------------------

    // NOTE: these read the node's axes in global space instead of the Scale property. Godot stores a
    // horizontal mirror either as scale (-1, 1) or as scale (1, -1) with rotation 180 degrees (it
    // re-decomposes the transform that way whenever the transform is set as a whole). Both look
    // identical on screen, and both give the same result here:
    //   local X axis points left on screen  -> mirrored horizontally -> reversed input
    //   local Y axis points up on screen    -> mirrored vertically   -> reversed gravity

    /// <summary>+1 = normal gravity (down), -1 = inverted gravity (node is flipped vertically).</summary>
    public float GravitySign => GlobalTransform.Y.Y < 0f ? -1f : 1f;

    /// <summary>+1 = normal input, -1 = reversed input (node is mirrored horizontally).</summary>
    private float HorizontalSign => GlobalTransform.X.X < 0f ? -1f : 1f;

    /// <summary>
    /// What the player actually sees: the sprite's own flip combined with the node being mirrored.
    /// Effects use this so they always face the same way as the character on screen.
    /// </summary>
    public bool VisuallyFlipped => facingFlipH ^ (HorizontalSign < 0f);

    /// <summary>
    /// World position of this character's feet: bottom-center of its collision shape, on the side
    /// gravity pulls toward. Because the point is converted through the shape's own transform, it
    /// automatically ends up at the top of the body for upside-down clones.
    /// Falls back to the node origin if there is no collision shape.
    /// </summary>
    public Vector2 FeetPosition
    {
        get
        {
            if (bodyShape?.Shape == null)
            {
                return GlobalPosition;
            }

            Rect2 rect = bodyShape.Shape.GetRect();
            Vector2 localFeet = new Vector2(rect.GetCenter().X, rect.End.Y);
            return bodyShape.ToGlobal(localFeet);
        }
    }

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
        facingFlipH = sprite?.FlipH ?? false;
        animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        animationTree = GetNodeOrNull<AnimationTree>("AnimationTree");

        // First CollisionShape2D child is treated as the body shape (used to find the feet).
        foreach (Node child in GetChildren())
        {
            if (child is CollisionShape2D shapeNode)
            {
                bodyShape = shapeNode;
                break;
            }
        }

        if (animationTree != null)
        {
            animationTree.AnimationFinished += OnAnimationFinished;
        }
        indicator = GetNode<AnimatedSprite2D>("indicator");

        // Make floor/ceiling detection match the gravity direction from the start.
        UpDirection = new Vector2(0, -GravitySign);

        _ = WireSignalsAsync();

        if (this is not Gorgonzola)
        {
            MainPlayerKilled += OnMainPlayerKilled;
        }

        if (animationTree != null)
        {
            animationTree.Active = true;
        }
    }

    /// <summary>
    /// Physics update loop. Handles input, movement, gravity application, and animation updates.
    /// </summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        float g = GravitySign;

        // Gravity direction comes from the vertical flip, so "up" for floor/ceiling checks must follow it.
        UpDirection = new Vector2(0, -g);

        Vector2 velocity = Velocity;

        if (!IsLevelCompleted() && !IsGamePaused())
        {
            // No input is read when the flag is off; direction stays 0 so the character stops horizontally.
            // Multiplying by the horizontal sign makes mirrored nodes move as they look.
            float direction = AllowPlayerInput ? GetMovementInput() * HorizontalSign : 0f;
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

        if (!groundStateInitialized)
        {
            isLanded = IsOnFloor();
            groundStateInitialized = true;
        }

        // Ceiling fix: when the head hits a ceiling, MoveAndSlide can leave residual velocity
        // pushing into it (especially when also pressed into a wall / corner). Kill it so gravity
        // starts pulling the character away on the very next frame.
        // "Up" is relative to gravity, so multiply by g.
        if (IsOnCeiling() && Velocity.Y * g < 0f)
        {
            Velocity = new Vector2(Velocity.X, 0f);
        }

        // Use the post-collision velocity so animations reflect what actually happened.
        UpdateAnimation(Velocity);

        // The indicator sits "above" the head, which is below on inverted characters.
        indicator.GlobalPosition = new Vector2(GlobalPosition.X, GlobalPosition.Y - offset * g);
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

        if (animationTree != null)
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

        if (gm != null && IsInsideTree())
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
    /// The jump always goes against gravity, so it is flipped for inverted characters.
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

            float g = GravitySign;

            // Work in "gravity space" (positive = falling) so inverted characters behave identically.
            float gravitySpaceVelocity = Math.Min(velocity.Y * g, -JumpVelocity);
            velocity.Y = gravitySpaceVelocity * g;

            Effect.SpawnEffectOriented(this, jumpEffectScene);
        }
    }

    /// <summary>
    /// Makes this controller jump immediately, ignoring input, the AllowPlayerInput flag,
    /// the jump buffer and whether it is on the ground. Jumps against gravity.
    /// </summary>
    /// <param name="strength">Optional jump velocity override. Defaults to JumpVelocity.</param>
    /// <param name="spawnEffect">Whether to spawn the jump effect.</param>
    public void ForceJump(float? strength = null, bool spawnEffect = true)
    {
        float jumpSpeed = strength ?? JumpVelocity;

        Velocity = new Vector2(Velocity.X, -jumpSpeed * GravitySign);
        jumpBufferTimer = 0f;

        if (spawnEffect)
        {
            Effect.SpawnEffectOriented(this, jumpEffectScene);
        }
    }

    /// <summary>
    /// Applies gravity to the character taking into account ascent and fall multipliers
    /// and clamps the fall speed to MaxFallSpeed. Plays landing effect when touching the floor.
    /// Gravity direction follows the vertical flip of the node.
    /// </summary>
    /// <param name="velocity">Character velocity, passed by reference to be modified.</param>
    /// <param name="dt">Delta time in seconds.</param>
    private void ApplyGravity(ref Vector2 velocity, float dt)
    {
        float g = GravitySign;

        // Gravity space: positive = moving with gravity (falling), negative = rising.
        float fall = velocity.Y * g;

        isFalling = fall >= 0f;

        if (IsOnFloor())
        {
            if (!isLanded)
            {
                Effect.SpawnEffectOriented(this, landEffectScene);
                isLanded = true;
            }
            return;
        }

        isLanded = false;

        // Bonked the ceiling: cancel any remaining upward speed immediately.
        if (IsOnCeiling() && fall < 0f)
        {
            fall = 0f;
        }

        // Add Mathf.Abs(Scale.Y) here if you also want the scale's magnitude to change gravity strength.
        float gravity = Math.Abs(GetGravity().Y);

        // Ascend multiplier only while genuinely rising; otherwise (including 0) use fall gravity.
        float multiplier = fall < 0f ? AscendMultiplier : FallMultiplier;
        fall += gravity * multiplier * dt;

        if (fall > MaxFallSpeed)
        {
            fall = MaxFallSpeed;
        }

        velocity.Y = fall * g;
    }

    /// <summary>
    /// Handles horizontal movement input and flipping the sprite if necessary.
    /// </summary>
    /// <param name="velocity">Character velocity, passed by reference to be modified.</param>
    /// <param name="direction">Horizontal input direction (-1..1), already adjusted for the horizontal mirror.</param>
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
            facingFlipH = ShouldFlipSprite(direction);
            if (sprite != null)
            {
                sprite.FlipH = facingFlipH;
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
                // Falling is relative to gravity, not screen-down.
                PlayAnimation(velocity.Y * GravitySign > 0f ? "isFalling" : "jump");
            }
        }
        else if (canMove)
        {
            if (this is Gorgonzola gorgonzola)
            {
                PlayAnimation("enter_door");
                gorgonzola.doorMoveTriggered = true;
            }
            else if (cloneType == CloneType.Shadow)
            {
                Kill();
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
    /// Called when an animation on the AnimationTree finishes. Handles transitions for
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
    /// Provides a horizontal movement input value (-1..1). Override in derived classes to change the source.
    /// Only called while AllowPlayerInput is true.
    /// </summary>
    /// <returns>Horizontal input direction.</returns>
    protected virtual float GetMovementInput()
    {
        // Always the raw player input. Reversal for a mirrored node is applied in _PhysicsProcess.
        return Input.GetAxis("move_left", "move_right");
    }

    /// <summary>
    /// Determines whether the sprite's FlipH should be set for the given movement direction.
    /// Pure function: it does not modify any state. Pass the direction already adjusted for the
    /// horizontal mirror (as HandleHorizontalMovement receives it).
    /// </summary>
    /// <param name="direction">Horizontal direction value (already multiplied by the horizontal sign).</param>
    /// <returns>True if the sprite should be flipped horizontally.</returns>
    public bool ShouldFlipSprite(float direction)
    {
        // A mirrored node already mirrors the sprite on screen, so FlipH only needs to follow the
        // raw input direction. The result is that the character always visually faces where it moves.
        float rawDirection = direction * HorizontalSign;
        return rawDirection < 0f;
    }

    /// <summary>
    /// Whether a jump was requested this physics frame. Override to change where jump input comes from.
    /// </summary>
    protected virtual bool IsJumpJustPressed() => Input.IsActionJustPressed("jump");

    /// <summary>
    /// Handles logic when this player/controller is killed. Releases keys and updates UI.
    /// </summary>
    public virtual void Kill()
    {
        if (killed)
        {
            return;
        }

        killed = true;

        if (killEffectScene != null)
        {
            // Instantiate exactly once.
            Node2D killEffect = killEffectScene.Instantiate<Node2D>();
            bool isGorg = this is Gorgonzola;
            GorgCarcass gorgCarcass = isGorg ? killEffect as GorgCarcass : null;
            bool facingLeft = VisuallyFlipped;

            // GorgCarcass reads flip in its _Ready, which runs as soon as it enters the tree,
            // so flip must be set BEFORE AddChild. It is true when facing left on screen.
            if (gorgCarcass != null)
            {
                gorgCarcass.flip = facingLeft;
            }

            GetTree().CurrentScene.AddChild(killEffect);
            killEffect.GlobalPosition = FeetPosition;

            if (!isGorg)
            {
                Effect.OrientEffect(this, killEffect);
            }

            // The shader is only ever applied to the death effect.
            Effect.ApplyMaterial(killEffect, sprite.Material);
        }

        QueueFree();

        if (hasKey)
        {
            hasKey = false;
            Door.GetInstance()?.Close();
            Key.GetInstance()?.BlackenUIElement();
        }
    }

    /// <summary>
    /// Called on game flush/reset. Resets transient state and queues the node for freeing.
    /// </summary>
    protected virtual void Flush()
    {
        isLanded = false;
        groundStateInitialized = false;
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