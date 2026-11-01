using Godot;
using System;
using System.Collections.Generic;

public partial class KillZone : Node2D
{
    // Node references
    private Area2D area;
    private HashSet<Node> processedBodies = new();

    public override void _Ready()
    {
        area = GetNode<Area2D>("Area2D");
        area.BodyEntered += OnBodyEntered;
        area.BodyExited += OnBodyExited;
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