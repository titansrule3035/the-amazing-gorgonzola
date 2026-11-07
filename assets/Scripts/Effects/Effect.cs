using Godot;
using System;

public partial class Effect : Node2D
{
    // Node references
    protected AnimationPlayer animPlayer;
    protected AnimatedSprite2D sprite;

    // Internal flags
    protected bool spriteConnected = false;
    protected bool animPlayerConnected = false;

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
            sprite.Play("default");
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

    /// <summary>
    /// Instantiates a particle/visual effect scene at the given world position and adds it to the tree.
    /// </summary>
    /// <param name="scene">PackedScene for the effect.</param>
    /// <param name="position">Global position to place the effect.</param>
    public static void SpawnEffect(Node2D owner, PackedScene scene, Vector2 position, bool shouldFlip)
    {
        if (scene == null)
        {
            return;
        }

        var effect = scene.Instantiate<Node2D>();
        if (effect == null)
        {
            return;
        }

        owner.GetParent<Node2D>().AddChild(effect);

        effect.GlobalPosition = position;

        var anim = effect.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (anim != null)
        {
            anim.FlipH = shouldFlip;
        }
    }

    public static void SpawnEffect(Node2D owner, PackedScene scene, Vector2 position, bool shouldFlip, Material shader)
    {
        if (scene == null)
        {
            return;
        }

        var effect = scene.Instantiate<Effect>();
        if (effect == null)
        {
            return;
        }

        owner.GetParent<Node2D>().AddChild(effect);

        effect.GlobalPosition = position;

        var anim = effect.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (anim != null)
        {
            anim.FlipH = shouldFlip;
        }
    }

    /// <summary>
    /// Instantiates an effect next to this character, always at its feet (bottom of its collision
    /// shape on the side gravity pulls toward), and orients it to match the character:
    /// mirrored horizontally if the character visually faces left, flipped vertically when
    /// gravity is inverted.
    /// </summary>
    public static Node2D SpawnEffectOriented(BasePlayerController owner, PackedScene scene)
    {
        if (scene == null)
        {
            return null;
        }

        Node2D fx = scene.Instantiate<Node2D>();
        owner.GetParent<Node2D>().AddChild(fx);
        fx.GlobalPosition = owner.FeetPosition;
        OrientEffect(owner, fx);
        return fx;
    }

    /// <summary>
    /// Puts the material on the effect's root and makes every CanvasItem below it inherit that
    /// material, so the shader shows on the effect's sprites too. Does nothing if material is null.
    /// </summary>
    public static void ApplyMaterial(Node2D fx, Material material)
    {
        if (fx == null || material == null)
        {
            return;
        }

        fx.Material = material;
        SetUseParentMaterial(fx);
    }

    private static void SetUseParentMaterial(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is CanvasItem item)
            {
                item.UseParentMaterial = true;
            }

            SetUseParentMaterial(child);
        }
    }

    /// <summary>
    /// Gives the effect the same on-screen orientation as the owner. If the effect's parent is itself
    /// mirrored, that mirroring is compensated so the result on screen is still correct.
    /// Must be called after the effect has been added to the tree.
    /// </summary>
    public static void OrientEffect(BasePlayerController owner, Node2D fx)
    {
        float x = owner.VisuallyFlipped ? -1f : 1f;
        float y = owner.GravitySign;

        // The effect inherits its parent's flip, so cancel it out here.
        if (fx.GetParent() is Node2D parent)
        {
            if (parent.GlobalTransform.X.X < 0f)
            {
                x = -x;
            }

            if (parent.GlobalTransform.Y.Y < 0f)
            {
                y = -y;
            }
        }

        fx.Scale = new Vector2(Mathf.Abs(fx.Scale.X) * x, Mathf.Abs(fx.Scale.Y) * y);
    }
}