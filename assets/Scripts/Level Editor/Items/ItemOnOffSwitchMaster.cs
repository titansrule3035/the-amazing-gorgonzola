using Godot;
using System;
using System.ComponentModel;

public partial class ItemOnOffSwitchMaster : EditorItemObject
{
    AnimationPlayer animationPlayer;

    [Export] PackedScene normalSwitch;

    PackedScene masterSwitch;

    static ItemOnOffSwitchMaster instance;

    public override void _Ready()
    {
        if (instance != null)
        {
            DebugManager.Log($"Only one OnOffSwitch item grid allowed, deleting this one...", "OnOffSwitchMasterScript");
            QueueFree();
            return;
        }

        instance = this;

        animationPlayer = GetNode<AnimationPlayer>("TextureAnimationPlayer");

        OnOffManager.OnStateChanged += OnStateChanged;

        masterSwitch = thisScene;

        base._Ready();
    }

    public override void _Process(double delta)
    {
        if (!GetTree().Paused)
        {
            DisableItem();
        }

        base._Process(delta);
    }

    void OnStateChanged(bool state)
    {
        GetNode<AnimationPlayer>("TextureAnimationPlayer").Play(state ? "green" : "red");
    }

    public void RegisterMasterSwitch(OnOffSwitchMaster masterSwitch)
    {
        thisScene = normalSwitch;

        if (selected)
        {
            SendItemToEditor();
        }
    }

    public void DeregisterMasterSwitch()
    {
        SendItemToEditor();

        if (selected)
        {
            thisScene = masterSwitch;
        }
    }

    public static ItemOnOffSwitchMaster GetInstance()
    {
        return instance;
    }

    public override void _ExitTree()
    {
        if (instance == this)
        {
            instance = null;
        }

        OnOffManager.OnStateChanged -= OnStateChanged;

        base._ExitTree();
    }
}
