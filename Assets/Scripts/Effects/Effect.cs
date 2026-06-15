using Godot;
using System;

public partial class Effect : Node2D
{
    // Node references
    private AnimationPlayer animPlayer;
    private AnimatedSprite2D sprite;

    // Internal flags
    private bool spriteConnected = false;
    private bool animPlayerConnected = false;

    /// <summary>
    /// Called when the node enters the scene tree. Attempts to find an AnimationPlayer or
    /// AnimatedSprite2D child and play its default animation. If neither is present the node frees itself.
    /// </summary>
    public override void _Ready()
    {
        animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        if (animPlayer != null)
        {
            animPlayer.AnimationFinished += OnAnimationFinished;
            animPlayerConnected = true;
            animPlayer.Play("default");
        }
        else if (sprite != null)
        {
            sprite.AnimationFinished += OnSpriteAnimationFinished;
            spriteConnected = true;
            sprite.Play();
        }
        else
        {
            QueueFree();
        }
    }

    /// <summary>
    /// Called when the AnimationPlayer finishes an animation. Frees this node.
    /// </summary>
    /// <param name="animName">Name of the finished animation.</param>
    private void OnAnimationFinished(StringName animName)
    {
        QueueFree();
    }

    /// <summary>
    /// Called when the AnimatedSprite2D finishes its animation. Frees this node.
    /// </summary>
    private void OnSpriteAnimationFinished()
    {
        QueueFree();
    }

    /// <summary>
    /// Cleans up subscribed events when exiting the scene tree.
    /// </summary>
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
