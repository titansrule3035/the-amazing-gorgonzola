using Godot;
using System;
using System.Collections.Generic;
using System.Text;

public partial class Enemy : RigidBody2D
{
    public static Action? KillAll;

    public override void _Ready()
    {
        KillAll += Kill;
    }

    public void Kill()
    {
        KillAll -= Kill;
        QueueFree();
    }

    public static void KillAllEnemies() => KillAll?.Invoke();
}

public class State()
{
    Action OnStateChanged;

    void Enter()
    {

    }
    void Exit()
    {

    }

    void Update()
    {

    }
}