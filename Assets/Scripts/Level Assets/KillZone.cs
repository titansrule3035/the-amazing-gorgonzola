using Godot;
using System;
using System.Collections.Generic;

public partial class KillZone : Node2D
{
    private Area2D _area;
    private HashSet<Node> processedBodies = new HashSet<Node>();

    public override void _Ready()
    {
        _area = GetNode<Area2D>("Area2D");
        _area.BodyEntered += OnBodyEntered;
        _area.BodyExited += OnBodyExited;
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