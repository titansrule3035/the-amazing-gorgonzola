using Godot;
using System;
using TheAmazingGorgonzola.assets.Scripts.Level_Assets;

public partial class OnOffSwitch : Node2D
{
    // Node references
    public AnimatedSprite2D sprite;

    // Exported state
    [Export] public bool opened;

    // Lifecycle
    public override void _Ready()
    {
        // Cache node references

        // Connect signals
        GetNode<Area2D>("Area2D").BodyEntered += OnBodyEntered;
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

        string state = "turn_" + (on ? "on" : "off");

        PlayAnimation(state);
    }

    // Play a local animation
    protected virtual void PlayAnimation(string animation)
    {
        GetNode<AnimatedSprite2D>("AnimatedSprite2D").Play(animation);
    }

    public override void _ExitTree()
    {
        GetNode<Area2D>("Area2D").BodyEntered -= OnBodyEntered;
        OnOffManager.OnStateChanged -= ChangeState;

        base._ExitTree();
    }
}