using Godot;
using System;

public partial class Effect : Node2D
{
    private AnimationPlayer animPlayer;
    private AnimatedSprite2D sprite;
    private bool spriteConnected = false;
    private bool animPlayerConnected = false;

    public override void _Ready()
    {
        animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        if (animPlayer != null)
        {
            animPlayer.AnimationFinished += OnAnimationFinished;
            animPlayerConnected = true;
            animPlayer.Play("default"); // play your default anim
        }
        else if (sprite != null)
        {
            sprite.AnimationFinished += OnSpriteAnimationFinished;
            spriteConnected = true;
            sprite.Play(); // plays default anim
        }
        else
        {
            QueueFree(); // no animation, free instantly
        }
    }

    private void OnAnimationFinished(StringName animName)
    {
        QueueFree();
    }

    private void OnSpriteAnimationFinished()
    {
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (animPlayerConnected && animPlayer != null)
        {
            animPlayer.AnimationFinished -= OnAnimationFinished;
            animPlayerConnected = false;
        }

        if (spriteConnected && sprite != null)
        {
            sprite.AnimationFinished -= OnSpriteAnimationFinished;
            spriteConnected = false;
        }

        base._ExitTree();
    }
}
