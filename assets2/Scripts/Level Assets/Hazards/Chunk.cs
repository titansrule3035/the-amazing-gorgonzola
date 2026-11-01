using Godot;
using System;
using System.Security.Cryptography.X509Certificates;

public partial class Chunk : Enemy
{
    RayCast2D rayLeft;
    RayCast2D rayRight;

    public Vector2 moveSpeed = new(100f, 0);

    public int directionModifier = 1;

    public override void _Ready()
    {
        KillAll += Kill;

        rayLeft = GetNode<RayCast2D>("ray_left");
        rayRight = GetNode<RayCast2D>("ray_right");

        base._Ready();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (rayLeft.IsColliding())
        {
            directionModifier = 1;
        }

        if (rayRight.IsColliding())
        {
            directionModifier = -1;
        }

        Position += moveSpeed * directionModifier * (float)delta;
    }
}