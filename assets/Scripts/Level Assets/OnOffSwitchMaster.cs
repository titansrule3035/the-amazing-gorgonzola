using Godot;
using System;
using TheAmazingGorgonzola.assets.Scripts.Level_Assets;
using static Godot.WebRtcDataChannel;

public partial class OnOffSwitchMaster : OnOffSwitch
{
    public override void _Ready()
    {
        if (opened != OnOffManager.GetState())
        {
            OnOffManager.SetState(opened);
        }

        base._Ready();
    }
}
