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
        sprite.Play($"{(state == green ? "close" : "open")}");
        UpdateBody(state);
    }

    // Enable or disable the collision shape according to color and global state
    private void UpdateBody(bool on)
    {
        GetNode<StaticBody2D>("StaticBody2D").CallDeferred(StaticBody2D.MethodName.SetCollisionLayerValue, 1, green == on);
        GetNode<StaticBody2D>("StaticBody2D").CallDeferred(StaticBody2D.MethodName.SetCollisionMaskValue, 1, green == on);
    }

    // Set initial animation and collision to match current global state
    void CheckState()
    {
        bool state = OnOffManager.GetState();
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        sprite.Play($"start_{(state == green ? "close" : "open")}");
        UpdateBody(state);
    }

    // Public helper so external objects (like switches) can force a refresh of this block's visuals/collision
    public void RefreshState()
    {
        CheckState();
    }
}