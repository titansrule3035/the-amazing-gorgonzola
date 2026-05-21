using Godot;
using System;

public partial class OnOffSwitch : Node2D
{
    Area2D collisionArea;
    AnimatedSprite2D sprite;

    bool opened;

    public override void _Ready()
    {
        collisionArea = GetNode<Area2D>("Area2D");
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        collisionArea.BodyEntered += OnBodyEntered;
        collisionArea.BodyExited += OnBodyExited;
        base._Ready();
    }

    void OnBodyEntered(Node2D body)
    {
        ChangeState();
    }

    void OnBodyExited(Node2D body)
    {

    }

    void ChangeState()
    {
        opened = !opened;
        if (opened)
        {
            sprite.Play("turn_on");
        }
        else
        {
            sprite.Play("turn_off");
        }
    }
}
