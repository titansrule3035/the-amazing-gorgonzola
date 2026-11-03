using Godot;
using System;

public partial class MissileMorty : Enemy
{
    public enum Directions { Left, Right }

    [Export] public Directions missileDirection = Directions.Right;
    [Export] public float missileSpeed = 200f;

    [ExportGroup("Death Fall")]
    [Export] public float crashSpinSpeed = 1.2f;

    [Export(PropertyHint.Range, "0,2000,10,or_greater")]
    public float crashFallDelayMs = 180f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public float stompMomentumRetention = 0.4f;

    [ExportGroup("Stomp Detection")]
    [Export] public float stompMargin = 0f;

    public RayCast2D ray;
    public Area2D stompArea;
    public AnimatedSprite2D sprite;
    public AnimatedSprite2D flameSprite;
    public CollisionShape2D collisionShape;

    private FlyState flyState;
    private CrashState crashState;
    private CrashFallState crashFallState;
    private DeathState deathState;

    public override void _Ready()
    {
        base._Ready();

        ray = GetNode<RayCast2D>("ray_cast");
        stompArea = GetNode<Area2D>("stomp_area");
        sprite = GetNode<AnimatedSprite2D>("sprite");
        flameSprite = GetNode<AnimatedSprite2D>("sprite_2");
        collisionShape = GetNode<CollisionShape2D>("collision_shape");

        GravityScale = 0f;

        stompArea.BodyEntered += OnBodyEnteredStompArea;
        sprite.AnimationFinished += OnSpriteAnimationFinished;
        flameSprite.AnimationFinished += OnFlameAnimationFinished;

        flyState = new FlyState(this);
        crashState = new CrashState(this);
        crashFallState = new CrashFallState(this);
        deathState = new DeathState(this);

        SwitchState(flyState);
    }

    public override void Kill()
    {
        EnterOnce(deathState);
    }

    private void OnSpriteAnimationFinished()
    {
        switch (sprite.Animation)
        {
            case "launch":
                flyState.OnLaunchFinished();
                break;
            case "crash":
                crashState.OnAnimationFinished();
                break;
        }
    }

    private void OnFlameAnimationFinished()
    {
        switch (flameSprite.Animation)
        {
            case "flame_on":
                flyState.OnFlameOn();
                break;
        }
    }

    private void OnBodyEnteredStompArea(Node2D body)
    {
        if (!flyState.Active)
        {
            return;
        }

        if (body is not BasePlayerController player)
        {
            return;
        }

        bool fallingDown = player.Velocity.Y > 0f;

        if (fallingDown)
        {
            player.ForceJump(null, true);
            Kill();
        }
    }

    private float DirectionSign => missileDirection == Directions.Right ? 1f : -1f;

    private abstract class MissileMortyState : EnemyState
    {
        protected readonly MissileMorty morty;

        protected MissileMortyState(MissileMorty morty) : base(morty)
        {
            this.morty = morty;
        }

        protected RayCast2D ray => morty.ray;
        protected Area2D stompArea => morty.stompArea;
        protected AnimatedSprite2D sprite => morty.sprite;
        protected AnimatedSprite2D flameSprite => morty.flameSprite;

        protected void DisableCollision()
        {
            ray.Enabled = false;
            stompArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
            stompArea.SetDeferred(Area2D.PropertyName.Monitorable, false);

            morty.GetNode<KillZone>("kill_zone").Enabled = false;

            foreach (Node child in morty.GetChildren())
            {
                if (child is CollisionShape2D shape)
                {
                    shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
                }
            }
        }
    }

    private sealed class FlyState : MissileMortyState
    {
        public bool Active { get; private set; }

        public FlyState(MissileMorty morty) : base(morty) { }

        public override void Enter()
        {
            Active = true;
            ApplyDirection();
        }

        public override void Exit()
        {
            Active = false;
        }

        public override void PhysicsUpdate(double delta)
        {
            ApplyDirection();

            if (ray.IsColliding())
            {
                var collider = ray.GetCollider();

                if (collider is BasePlayerController player)
                {
                    player.CallDeferred("Kill");
                    return;
                }

                if (collider is Node && collider is not Area2D)
                {
                    morty.EnterOnce(morty.crashState);
                    return;
                }
            }

            morty.Position += new Vector2(morty.DirectionSign * morty.missileSpeed, 0) * (float)delta;
        }

        private void ApplyDirection()
        {
            morty.Scale = new Vector2(morty.DirectionSign, 1f);
        }

        public void OnLaunchFinished()
        {
            flameSprite.Play("flame_on");
        }

        public void OnFlameOn()
        {
            sprite.Play("fly");
            flameSprite.Play("flame_idle");
            morty.GetNode<KillZone>("kill_zone").Enabled = true;
        }
    }

    private sealed class CrashState : MissileMortyState
    {
        private bool active;
        private bool animationDone;
        private float delayTimer;

        public CrashState(MissileMorty morty) : base(morty) { }

        public override void Enter()
        {
            active = true;
            animationDone = false;

            sprite.Play("crash"); // must match your SpriteFrames, looping off
            flameSprite.Play("flame_off");
            DisableCollision();
        }

        public override void Exit()
        {
            active = false;
        }

        public void OnAnimationFinished()
        {
            if (!active || animationDone || sprite.Animation != "crash") return;

            animationDone = true;
            delayTimer = morty.crashFallDelayMs / 1000f;
        }

        public override void PhysicsUpdate(double delta)
        {
            if (!animationDone) return;

            delayTimer -= (float)delta;
            if (delayTimer <= 0f)
            {
                morty.SwitchState(morty.crashFallState);
            }
        }
    }

    // Wall crash -> straight drop, no animation change
    private sealed class CrashFallState : MissileMortyState
    {
        public CrashFallState(MissileMorty morty) : base(morty) { }

        public override void Enter()
        {
            bool facingRight = morty.missileDirection == Directions.Right;

            morty.fallMotion.Start(velocityX: 0f, spinDirection: facingRight ? -1f : 1f, spinSpeedOverride: morty.crashSpinSpeed);
            DisableCollision();
        }

        public override void PhysicsUpdate(double delta)
        {
            morty.fallMotion.Tick(delta);
        }
    }

    // Stomped or killed: plays "death", keeps some momentum
    private sealed class DeathState : MissileMortyState
    {
        public DeathState(MissileMorty morty) : base(morty) { }

        public override void Enter()
        {
            bool facingRight = morty.missileDirection == Directions.Right;

            morty.fallMotion.Start(velocityX: morty.DirectionSign * morty.missileSpeed * morty.stompMomentumRetention, spinDirection: facingRight ? 1f : -1f);
            sprite.Play("death");
            flameSprite.Play("flame_off");
            DisableCollision();
        }

        public override void PhysicsUpdate(double delta)
        {
            morty.fallMotion.Tick(delta);
        }
    }
}