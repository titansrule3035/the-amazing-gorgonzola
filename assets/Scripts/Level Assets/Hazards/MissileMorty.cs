using Godot;
using System;

public partial class MissileMorty : Enemy
{
    public enum Directions
    {
        Left,
        Right
    }

    [Export] public Directions missileDirection = Directions.Right;
    [Export] public float missileSpeed = 200f;
    [Export] public Vector2 missileVelocity = new Vector2(200f, 0);

    [ExportGroup("Death Fall")]
    [Export] public float fallGravity = 400f;
    [Export] public float fallSpeed = 340f;
    [Export] public float deathDuration = 3f;
    [Export] public float horizontalDrag = 150f;
    [Export] public float spinSpeed = 2.5f;
    [Export] public float crushSpinSpeed = 1.2f;

    [Export(PropertyHint.Range, "0,2000,10,or_greater")]
    public float crushFallDelayMs = 180f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public float stompMomentumRetention = 0.4f;

    [ExportGroup("Stomp Detection")]
    [Export] public float stompMargin = 0f;

    private RayCast2D ray;
    private Area2D area;
    private AnimatedSprite2D sprite;

    bool crushed = false;
    bool killed = false;

    private float fallVelocity = 0f;
    private float deathVelocityX = 0f;
    private float deathTimer = 0f;
    private float spinDirection = 1f;
    private float currentSpinSpeed = 0f;
    private bool crushAnimationDone = false;
    private float crushDelayTimer = 0f;

    public override void _Ready()
    {
        ray = GetNode<RayCast2D>("ray_cast");
        area = GetNode<Area2D>("area_2d");
        sprite = GetNode<AnimatedSprite2D>("sprite");

        GravityScale = 0f;

        area.BodyEntered += OnBodyEntered;
        sprite.AnimationFinished += OnSpriteAnimationFinished;

        CheckDirection();

        base._Ready();
    }

    public override void _Process(double delta)
    {
        CheckDirection();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (killed)
        {
            fallVelocity = Math.Min(fallVelocity + fallGravity * dt, fallSpeed);

            deathVelocityX = Mathf.MoveToward(deathVelocityX, 0f, horizontalDrag * dt);

            Position += new Vector2(deathVelocityX, fallVelocity) * dt;

            Rotation += spinDirection * currentSpinSpeed * dt;

            deathTimer -= dt;
            if (deathTimer <= 0f)
            {
                QueueFree();
            }
            return;
        }

        if (crushed)
        {
            if (crushAnimationDone)
            {
                crushDelayTimer -= dt;
                if (crushDelayTimer <= 0f)
                {
                    StartFalling(null, 0f);
                }
            }
            return;
        }

        if (ray.IsColliding())
        {
            if (ray.GetCollider() is BasePlayerController player)
            {
                player.Kill();
            }
            else if (ray.GetCollider() is Node)
            {
                crushed = true;
                sprite.Play("crush");
                return;
            }
        }

        Position += missileVelocity * dt;
    }

    void CheckDirection()
    {
        if (!crushed && !killed)
        {
            if (missileDirection == Directions.Left)
            {
                Scale = new Vector2(-1, 1);
                missileVelocity = new Vector2(-missileSpeed, 0);
            }
            else
            {
                Scale = Vector2.One;
                missileVelocity = new Vector2(missileSpeed, 0);
            }
        }
        else
        {
            missileVelocity = Vector2.Zero;
        }
    }

    void OnSpriteAnimationFinished()
    {
        if (crushed && !killed && !crushAnimationDone && sprite.Animation == "crush")
        {
            crushAnimationDone = true;
            crushDelayTimer = crushFallDelayMs / 1000f;
        }
    }

    void OnBodyEntered(Node2D body)
    {
        if (killed || crushed)
        {
            return;
        }

        if (body is not BasePlayerController player)
        {
            return;
        }

        bool fallingDown = player.Velocity.Y > 0f;
        bool isAbove = player.GlobalPosition.Y < GlobalPosition.Y - stompMargin;

        if (fallingDown && isAbove)
        {
            player.ForceJump(null, false);
            StartFalling("death", stompMomentumRetention);
        }
    }
    void StartFalling(string? animation, float momentumRetention)
    {
        deathVelocityX = missileVelocity.X * momentumRetention;

        bool facingRight = missileDirection == Directions.Right;
        spinDirection = (facingRight ^ crushed) ? 1f : -1f;
        currentSpinSpeed = crushed ? crushSpinSpeed : spinSpeed;

        killed = true;
        deathTimer = deathDuration;
        fallVelocity = 0f;

        if (animation != null)
        {
            sprite.Play(animation);
        }
        ray.Enabled = false;
        area.SetDeferred(Area2D.PropertyName.Monitoring, false);
        area.SetDeferred(Area2D.PropertyName.Monitorable, false);

        foreach (Node child in GetChildren())
        {
            if (child is CollisionShape2D shape)
            {
                shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
            }
        }
    }
}