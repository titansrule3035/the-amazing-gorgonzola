using Godot;
using System;
using TheAmazingGorgonzola.assets.Scripts.Level_Assets;
using static Godot.WebRtcDataChannel;

public partial class OnOffSwitchMaster : OnOffSwitch
{
    /// <summary>
    /// Ensure the manager state matches this master switch on startup, then call base Ready.
    /// </summary>
    public override void _Ready()
    {
        if (opened != OnOffManager.GetState())
        {
            OnOffManager.SetState(opened);
        }

        base._Ready();
    }
}
