using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class KillZone : Area2D
{
    [Export] public bool Enabled = true;

    private HashSet<Node> processedBodies = new();

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        CollisionShape2D collisionShape = GetNode<CollisionShape2D>("collision_shape_2d");

        collisionShape.Disabled = !Enabled;

        if (Engine.IsEditorHint())
        {
            if (collisionShape != null)
            {
                collisionShape.DebugColor = new Color(1, 0, 0, 0.5f);
            }
        }
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