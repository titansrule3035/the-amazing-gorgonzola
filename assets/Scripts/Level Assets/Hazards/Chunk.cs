using Godot;
using System;

public partial class Chunk : Enemy
{
    [Export(PropertyHint.Range, "0,1,0.05")]
    public float deathMomentumRetention = 0.4f;

    public RayCast2D rayLeft;
    public RayCast2D rayRight;

    public Vector2 moveSpeed = new(100f, 0);

    public int directionModifier = 1;

    public AnimatedSprite2D sprite;

    // states
    private MoveState moveState;
    private DeathState deathState;

    public override void _Ready()
    {
        base._Ready();

        rayLeft = GetNode<RayCast2D>("ray_left");
        rayRight = GetNode<RayCast2D>("ray_right");
        sprite = GetNode<AnimatedSprite2D>("sprite"); // adjust to your node name

        moveState = new MoveState(this);
        deathState = new DeathState(this);

        SwitchState(moveState);
    }

    public override void Kill()
    {
        EnterOnce(deathState);
    }

    private abstract class ChunkState : EnemyState
    {
        protected readonly Chunk chunk;

        protected ChunkState(Chunk chunk) : base(chunk)
        {
            this.chunk = chunk;
        }

        protected RayCast2D rayLeft => chunk.rayLeft;
        protected RayCast2D rayRight => chunk.rayRight;
        protected AnimatedSprite2D sprite => chunk.sprite;
        protected Vector2 moveSpeed => chunk.moveSpeed;

        protected int directionModifier
        {
            get => chunk.directionModifier;
            set => chunk.directionModifier = value;
        }
    }

    private sealed class MoveState : ChunkState
    {
        public MoveState(Chunk chunk) : base(chunk) { }

        public override void Enter()
        {
            rayLeft.Enabled = rayRight.Enabled = true;
        }

        public override void Exit()
        {
            rayLeft.Enabled = rayRight.Enabled = false;
        }

        public override void PhysicsUpdate(double delta)
        {
            if (rayLeft.IsColliding())
            {
                directionModifier = 1;
            }

            if (rayRight.IsColliding())
            {
                directionModifier = -1;
            }

            chunk.GlobalPosition += moveSpeed * directionModifier * (float)delta;
        }
    }

    private sealed class DeathState : ChunkState
    {
        public DeathState(Chunk chunk) : base(chunk) { }

        public override void Enter()
        {
            sprite.Play("death");

            chunk.GetNode<CollisionShape2D>("collision_shape_2d").SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

            chunk.fallMotion.Start(velocityX: directionModifier * moveSpeed.X * chunk.deathMomentumRetention, spinDirection: directionModifier);
        }

        public override void PhysicsUpdate(double delta)
        {
            chunk.fallMotion.Tick(delta);
        }
    }
}