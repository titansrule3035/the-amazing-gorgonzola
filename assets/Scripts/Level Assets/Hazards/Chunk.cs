using Godot;
using System;
using System.Security.Cryptography.X509Certificates;

public partial class Chunk : Enemy
{
    public RayCast2D rayLeft;
    public RayCast2D rayRight;

    public Vector2 moveSpeed = new(100f, 0);

    public int directionModifier = 1;

    public AnimatedSprite2D sprite;

    //states
    MoveState moveState;
    DeathState deathState;

    bool killed = false;

    public override void _Ready()
    {
        base._Ready();

        rayLeft = GetNode<RayCast2D>("ray_left");
        rayRight = GetNode<RayCast2D>("ray_right");

        //initialize states
        moveState = new MoveState(this);
        deathState = new DeathState(this);

        SwitchState(moveState);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (killed)
        {
            SwitchState(deathState);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    public override void Kill()
    {
        SwitchState(deathState);
    }

    class ChunkState : EnemyState
    {
        protected Chunk chunk;
        public ChunkState(Chunk chunk) : base(chunk)
        {
            this.chunk = chunk;
        }

        protected RayCast2D rayLeft => chunk.rayLeft;
        protected RayCast2D rayRight => chunk.rayRight;

        protected int directionModifier
        {
            get => chunk.directionModifier;
            set => chunk.directionModifier = value;
        }

        protected Vector2 moveSpeed => chunk.moveSpeed;

        protected AnimatedSprite2D sprite => chunk.sprite;

        protected bool killed
        {
            get => chunk.killed;
            set => chunk.killed = value;
        }
    }

    class MoveState : ChunkState
    {
        public MoveState(Chunk chunk) : base(chunk)
        {
            
        }

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

    class DeathState : ChunkState
    {
        public DeathState(Chunk chunk) : base(chunk)
        {

        }

        public override async void Enter()
        {
            sprite.Play("death");
            chunk.GetNode<CollisionShape2D>("collision_shape_2d").Disabled = true;

            await chunk.ToSignal(chunk.GetTree().CreateTimer(3.0f, false), SceneTreeTimer.SignalName.Timeout);

            chunk.QueueFree();
        }
    }
}