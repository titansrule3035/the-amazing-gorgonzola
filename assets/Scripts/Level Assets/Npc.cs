using Godot;
using System;

public partial class Npc : Node2D
{
    public enum NpcType
    {
        Blinky,
        Buj,
        Esse,
        Fleshebry,
        Gergenschpooglus,
        Jub,
        Lixe,
        Pasghetti,
        Peter,
        Moonman,
        Ration,
        TheBoat,
        Smeagol
    }

    [Export] public NpcType npcType;
}
