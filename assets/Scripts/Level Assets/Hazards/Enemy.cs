using Godot;
using System;

public partial class Enemy : RigidBody2D
{
    public static Action? KillAll;

    [ExportGroup("Death Fall")]
    [Export] public float fallGravity = 400f;
    [Export] public float fallSpeed = 340f;
    [Export] public float deathDuration = 3f;
    [Export] public float horizontalDrag = 150f;
    [Export] public float spinSpeed = 2.5f;

    public FallMotion fallMotion { get; private set; }

    private EnemyState currentState;
    protected bool dying = false;

    public override void _Ready()
    {
        KillAll += Kill;
        fallMotion = new FallMotion(this);
    }

    public virtual void Kill()
    {
        QueueFree();
    }

    public override void _ExitTree()
    {
        KillAll -= Kill;
    }

    public override void _Process(double delta)
    {
        if (currentState == null)
        {
            GD.Print($"No current state set on {GetType()}.");
            return;
        }

        currentState.Update(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (currentState == null)
        {
            GD.Print($"No current state set on {GetType()}.");
            return;
        }

        currentState.PhysicsUpdate(delta);
    }

    protected void SwitchState(EnemyState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    // Guards against double transitions (e.g. killed twice, or killed while crashing)
    protected void EnterOnce(EnemyState next)
    {
        if (dying) return;
        dying = true;
        SwitchState(next);
    }

    public static void KillAllEnemies() => KillAll?.Invoke();

    /// <summary>
    /// Shared falling/spinning motion. Not a state: call Start() from a state's Enter()
    /// and Tick() from its PhysicsUpdate(). Frees the enemy when deathDuration runs out.
    /// </summary>
    public sealed class FallMotion
    {
        private readonly Enemy enemy;
        private float fallVelocity;
        private float velocityX;
        private float timer;
        private float spinDirection;
        private float currentSpinSpeed;

        public FallMotion(Enemy enemy)
        {
            this.enemy = enemy;
        }

        /// <param name="velocityX">Initial horizontal velocity (decays via horizontalDrag).</param>
        /// <param name="spinDirection">+1 clockwise, -1 counter-clockwise.</param>
        /// <param name="spinSpeedOverride">Radians/sec; null uses the enemy's spinSpeed export.</param>
        public void Start(float velocityX, float spinDirection, float? spinSpeedOverride = null)
        {
            this.velocityX = velocityX;
            this.spinDirection = spinDirection;
            currentSpinSpeed = spinSpeedOverride ?? enemy.spinSpeed;
            fallVelocity = 0f;
            timer = enemy.deathDuration;

            // We move the body manually, so keep the physics engine out of the way
            enemy.GravityScale = 0f;
            enemy.LinearVelocity = Vector2.Zero;
        }

        public void Tick(double delta)
        {
            float dt = (float)delta;

            fallVelocity = Math.Min(fallVelocity + enemy.fallGravity * dt, enemy.fallSpeed);
            velocityX = Mathf.MoveToward(velocityX, 0f, enemy.horizontalDrag * dt);

            enemy.Position += new Vector2(velocityX, fallVelocity) * dt;
            enemy.Rotation += spinDirection * currentSpinSpeed * dt;

            timer -= dt;
            if (timer <= 0f)
            {
                enemy.QueueFree();
            }
        }
    }

    public abstract class EnemyState
    {
        public Enemy enemy;

        public EnemyState(Enemy enemy)
        {
            this.enemy = enemy;
        }

        public virtual void Enter() { }

        public virtual void Exit() { }

        public virtual void Update(double delta) { }

        public virtual void PhysicsUpdate(double delta) { }
    }
}