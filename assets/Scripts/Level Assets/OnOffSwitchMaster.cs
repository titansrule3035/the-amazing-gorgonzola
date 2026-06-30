using Godot;
using System;
using TheAmazingGorgonzola.assets.Scripts.Level_Assets;
using static Godot.WebRtcDataChannel;

public partial class OnOffSwitchMaster : OnOffSwitch
{

    private static OnOffSwitchMaster instance;

    /// <summary>
    /// Ensure the manager state matches this master switch on startup, then call base Ready.
    /// </summary>
    public override void _Ready()
    {
        if (instance != null)
        {
            GD.PrintErr("Only one OnOff Master Switch allowed per scene, deleting this one...");
            QueueFree();
            return;
        }

        instance = this;

        if (opened != OnOffManager.GetState())
        {
            OnOffManager.SetState(opened);
        }

        OnOffManager.UpdateAllBlocks();

        base._Ready();
    }

    public override void _Process(double delta)
    {

        base._Process(delta);
    }

    /// <summary>
    /// Set the global on/off state to the specified value and update all blocks.
    /// The manager will raise the state-changed event which updates this switch's
    /// visual state as well as any registered blocks.
    /// </summary>
    /// <param name="state">Desired on/off state.</param>
    public void SetState(bool state)
    {
        // Avoid unnecessary work if state already matches
        if (OnOffManager.GetState() == state)
            return;

        OnOffManager.SetState(state);
        OnOffManager.UpdateAllBlocks();
        if (state)
        {
            sprite.Play("on");
        }
        else
        {
            sprite.Play("off");
        }
    }

    public static OnOffSwitchMaster GetInstance()
    {
        return instance;
    }
}
