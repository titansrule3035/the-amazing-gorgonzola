using Godot;
using System;
using System.Collections.Generic;

namespace TheAmazingGorgonzola.assets.Scripts.Level_Assets
{
    public partial class OnOffManager : Node2D
    {
        // Current state
        public static bool on = false;
        // Registered switches (not currently used but kept for future use)
        public static HashSet<OnOffSwitch> Switches = new HashSet<OnOffSwitch>();
        // Event raised when the state changes
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