using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class KillZone : Area2D
{
    [Export] public bool Enabled = true;

    private HashSet<Node> processedBodies = new();
    CollisionShape2D collisionShape;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        collisionShape = GetNode<CollisionShape2D>("collision_shape_2d");
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() && collisionShape != null)
        {
            collisionShape.DebugColor = new Color(1, 0, 0, 0.5f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        collisionShape.Disabled = !Enabled;
    }

    private void OnBodyEntered(Node body)
    {
        if (processedBodies.Contains(body))
        {
            return;
        }

        processedBodies.Add(body);

        if (body is BasePlayerController player)
        {
            player.CallDeferred("Kill");
        }
    }

    private void OnBodyExited(Node body)
    {
        processedBodies.Remove(body);
    }
}