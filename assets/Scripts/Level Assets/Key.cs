using Godot;
using System;

public partial class Key : Node2D
{
    Area2D area;
    public override void _Ready()
    {
        area = GetNode<Area2D>("Area2D");
        area.BodyEntered += OnBodyEntered;
        base._Ready();
    }
    private void OpenDoor()
    {
        var door = Door.GetInstance();
        if (door != null)
        {
            door.Open();
        }
    }
    private void OnBodyEntered(Node2D body)
    {
        if (body is BasePlayerController)
        {
            OpenDoor();

            (body as BasePlayerController).hasKey = true;

            QueueFree();
        }
    }
}
