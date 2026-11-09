using Godot;
using System;

public partial class OnOffBlock : Node2D
{
    // Exported properties (tweakable in the editor)
    [Export] public bool green;

    // Cached child node references
    private AnimatedSprite2D sprite;

    // Lifecycle
    public override void _Ready()
    {
        // Ensure this block is discoverable via groups so switches can refresh all blocks
        AddToGroup("OnOffBlock");
        // Cache nodes for quicker access
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Listen for global on/off state changes
        OnOffManager.OnStateChanged += OnStateChanged;

        // Initialize visuals/collision based on current state
        CheckState();

        base._Ready();
    }

    public override void _ExitTree()
    {
        // Unsubscribe to avoid dangling event handlers
        OnOffManager.OnStateChanged -= OnStateChanged;

        base._ExitTree();
    }

    // State handling
    // Called when the global on/off state changes
    void OnStateChanged(bool on)
    {
        bool state = OnOffManager.GetState();
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        if (state)
        {
            if (green)
            {
                sprite.Play("close");

            }
            else
            {
                sprite.Play("open");
            }
        }
        else
        {
            if (green)
            {
                sprite.Play("open");

            }
            else
            {
                sprite.Play("close");
            }
        }
    }

    // Enable or disable the collision shape according to color and global state
    private void UpdateBody(bool on)
    {
        StaticBody2D body = GetNode<StaticBody2D>("StaticBody2D");

        if (on)
        {
            body.CallDeferred(StaticBody2D.MethodName.SetCollisionLayerValue, 1, green ? false : true);
        }
        else
        {
            body.CallDeferred(StaticBody2D.MethodName.SetCollisionLayerValue, 1, green ? true : false);
        }
    }

    // Set initial animation and collision to match current global state
    void CheckState()
    {
        bool state = OnOffManager.GetState();
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
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

    // Public helper so external objects (like switches) can force a refresh of this block's visuals/collision
    public void RefreshState()
    {
        CheckState();
    }
}