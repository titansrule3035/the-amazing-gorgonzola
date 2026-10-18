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
            GD.Print("Only one OnOffSwitch item grid allowed, deleting this one...");
            QueueFree();
            return;
        }

        instance = this;

        animationPlayer = GetNode<AnimationPlayer>("TextureAnimationPlayer");

        OnOffManager.OnStateChanged += OnStateChanged;

        masterSwitch = ThisScene;

        base._Ready();
    }

    void OnStateChanged(bool state)
    {
        animationPlayer.Play(state ? "green" : "red");
    }

    public void RegisterMasterSwitch(OnOffSwitchMaster masterSwitch)
    {
        ThisScene = normalSwitch;
        SendItemToEditor();
    }

    public void DeregisterMasterSwitch()
    {
        ThisScene = masterSwitch;
        SendItemToEditor();
    }

    public static ItemOnOffSwitchMaster GetInstance()
    {
        return instance;
    }
}
