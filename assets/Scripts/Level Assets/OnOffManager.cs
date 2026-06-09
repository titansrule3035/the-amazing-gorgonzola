using Godot;
using System;
using System.Collections.Generic;

namespace TheAmazingGorgonzola.assets.Scripts.Level_Assets
{
    public partial class OnOffManager : Node2D
    {
        public static bool on = false;
        public static HashSet<OnOffSwitch> Switches = new HashSet<OnOffSwitch>();
        public static Action<bool> OnStateChanged;

        public static void ChangeState()
        {
            on = !on;
            OnStateChanged?.Invoke(on);
        }

        public static bool GetState()
        {
            return on;
        }

        public static void SetState(bool state)
        {
            on = state;
            OnStateChanged?.Invoke(on);
        }
    }
}