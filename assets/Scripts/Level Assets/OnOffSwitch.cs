using Godot;
using System;
using TheAmazingGorgonzola.assets.Scripts.Level_Assets;

public partial class OnOffSwitch : Node2D
{
    // Node references
    public Area2D collisionArea;
    public AnimatedSprite2D sprite;

    // Exported state
    [Export] public bool opened;

    // Lifecycle
    public override void _Ready()
    {
        // Cache node references
        collisionArea = GetNode<Area2D>("Area2D");
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Connect signals
        collisionArea.BodyEntered += OnBodyEntered;
        OnOffManager.OnStateChanged += ChangeState;

        // Initialize visual state
        opened = OnOffManager.GetState();
        PlayAnimation(opened ? "on" : "off");

        base._Ready();
    }

    // Signal handlers
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

    // Called when the global on/off state changes
    protected virtual void ChangeState(bool on)
    {
        opened = on;
        sprite.Play(on ? "turn_on" : "turn_off");
    }

    // Play a local animation
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