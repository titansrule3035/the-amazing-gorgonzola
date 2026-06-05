using Godot;
using System;
using TheAmazingGorgonzola.assets.Scripts.Level_Assets;

public partial class OnOffSwitch : Node2D
{
    public Area2D collisionArea;
    public AnimatedSprite2D sprite;
    [Export] public bool opened;

    public override void _Ready()
    {
        collisionArea = GetNode<Area2D>("Area2D");
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        collisionArea.BodyEntered += OnBodyEntered;

        opened = OnOffManager.GetState();

        if (opened)
        {
            PlayAnimation("on");
        }
        else
        {
            PlayAnimation("off");
        }

        OnOffManager.OnStateChanged += ChangeState;

        base._Ready();
    }

    protected void OnBodyEntered(Node2D body)
    {
        if (body is BasePlayerController)
        {
            BasePlayerController clone = body as BasePlayerController;
            if (!clone.isFalling)
            {
                OnOffManager.ChangeState();
            }
        }
    }

    protected virtual void ChangeState(bool on)
    {
        opened = on;
        sprite.Play(on ? "turn_on" : "turn_off");
    }

    protected virtual void PlayAnimation(string animation)
    {
        sprite.Play(animation);
    }

    public override void _ExitTree()
    {
        collisionArea.BodyEntered -= OnBodyEntered;

        OnOffManager.OnStateChanged -= ChangeState;

        base._ExitTree();
    }
}