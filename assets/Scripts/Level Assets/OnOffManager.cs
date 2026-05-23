using Godot;
using System;
using System.Collections.Generic;

namespace TheAmazingGorgonzola.assets.Scripts.Level_Assets
{
    public partial class OnOffManager : Node2D
    {
        [Export] public bool on = false;
        private static OnOffManager instance;
        public HashSet<OnOffSwitch> Switches = new HashSet<OnOffSwitch>();
        public Action<bool> OnStateChanged;

        public override void _Ready()
        {
            if (instance != null)
            {
                GD.PrintErr("More than one instance of OnOffManager was found in the scene! Deleting this one...");
                QueueFree();
                return;
            }
            instance = this;
        }

        public override void _ExitTree()
        {
            if (instance == this)
                instance = null;
            base._ExitTree();
        }

        public void ChangeState()
        {
            on = !on;
            OnStateChanged?.Invoke(on);
        }

        public bool GetState()
        {
            return on;
        }

        public void SetState(bool state)
        {
            on = state;
            OnStateChanged?.Invoke(on);
        }

        public static OnOffManager GetInstance()
        {
            return instance;
        }
    }
}