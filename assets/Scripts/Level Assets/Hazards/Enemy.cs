using Godot;
using System;
using System.Collections.Generic;
using System.Text;

public partial class Enemy : RigidBody2D
{
    public static Action? KillAll;

    private List<EnemyState> states = new List<EnemyState>();

    private EnemyState currentState;

    public override void _Ready()
    {
        KillAll += Kill;
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
            GD.Print($"No current state set on {this.GetType().ToString()}.");
            return;
        }

        currentState.Update(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (currentState == null)
        {
            GD.Print($"No current state set on {this.GetType().ToString()}.");
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

    public static void KillAllEnemies() => KillAll?.Invoke();

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