using Godot;
using System;
using TheAmazingGorgonzola.assets.Scripts.Level_Assets;

public partial class OnOffSwitch : Node2D
{
    Area2D collisionArea;
    AnimatedSprite2D sprite;
    [Export] public bool opened;

    public override void _Ready()
    {
        collisionArea = GetNode<Area2D>("Area2D");
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        collisionArea.BodyEntered += OnBodyEntered;

        var manager = OnOffManager.GetInstance();
        if (manager == null)
        {
            GD.PrintErr("OnOffSwitch: No OnOffManager instance found!");
            return;
        }


        if (opened != manager.GetState())
        {
            manager.SetState(opened);
        }

        if (opened)
        {
            PlayAnimation("on");
        }
        else
        {
            PlayAnimation("off");
        }

        manager.OnStateChanged += ChangeState;

        base._Ready();
    }

    void OnBodyEntered(Node2D body)
    {
        if (body is BasePlayerController)
        {
            BasePlayerController clone = body as BasePlayerController;
            if (!clone.isFalling)
            {
                OnOffManager.GetInstance().ChangeState();
            }
        }
    }

    void ChangeState(bool on)
    {
        opened = on;
        sprite.Play(on ? "turn_on" : "turn_off");
    }

    void PlayAnimation(string animation)
    {
        sprite.Play(animation);
    }

    public override void _ExitTree()
    {
        collisionArea.BodyEntered -= OnBodyEntered;

        var manager = OnOffManager.GetInstance();
        if (manager != null)
        {
            manager.OnStateChanged -= ChangeState;
        }
        base._ExitTree();
    }
}