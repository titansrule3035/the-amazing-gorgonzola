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

        masterSwitch = thisScene;

        base._Ready();
    }

    void OnStateChanged(bool state)
    {
        animationPlayer.Play(state ? "green" : "red");
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

        base._ExitTree();
    }
}
