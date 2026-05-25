using Godot;
using System;
using TheAmazingGorgonzola.assets.Scripts.Level_Assets;

public partial class OnOffBlock : Node2D
{
    [Export] public bool green;
    AnimatedSprite2D sprite;
    CollisionShape2D body;

    public override void _Ready()
    {
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        body = GetNode<StaticBody2D>("StaticBody2D").GetNode<CollisionShape2D>("CollisionShape2D");

        var manager = OnOffManager.GetInstance();
        if (manager == null)
        {
            GD.PrintErr("OnOffBlock: No OnOffManager instance found!");
            return;
        }

        manager.OnStateChanged += OnStateChanged;

        CheckState();

        base._Ready();
    }

    void OnStateChanged(bool on)
    {
        UpdateBody(on);
        if (on)
        {
            sprite.Play(green ? "close" : "open");
        }
        else
        {
            sprite.Play(green ? "open" : "close");
        }
    }

    private void UpdateBody(bool on)
    {
        if (on)
        {
            if (green)
            {
                body.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
            }
            else
            {
                body.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
            }
        }
        else
        {
            if (!green)
            {
                body.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
            }
            else
            {
                body.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
            }
        }
    }
    public override void _ExitTree()
    {
        var manager = OnOffManager.GetInstance();
        if (manager != null)
        {
            manager.OnStateChanged -= OnStateChanged;
        }
        base._ExitTree();
    }

    void CheckState()
    {
        bool state = OnOffManager.GetInstance().GetState();
        if (state)
        {
            if (green)
            {
                sprite.Play("start_close");

            }
            else
            {
                sprite.Play("start_open");
            }
        }
        else
        {
            if (green)
            {
                sprite.Play("start_open");

            }
            else
            {
                sprite.Play("start_close");
            }
        }
        UpdateBody(state);
    }
}