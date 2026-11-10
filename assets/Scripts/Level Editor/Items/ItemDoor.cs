using Godot;
using System;

public partial class ItemDoor : EditorItemObject
{
    private EditorGameManager gm;

    public override void _Ready()
    {
        base._Ready();
        gm = EditorGameManager.GetInstance();
        if (gm != null)
        {
            // Subscribe to gorg events and set initial state based on whether a gorg already exists
            gm.OnDoorRegistered += OnDoorRegistered;
            gm.OnDoorUnregistered += OnDoorUnregistered;

            if (gm.door != null)
            {
                DisableItem();
            }
            else
            {
                EnableItem();
            }
        }
    }

    public override void _ExitTree()
    {
        gm.OnDoorRegistered -= OnDoorRegistered;
        gm.OnDoorUnregistered -= OnDoorUnregistered;

        base._ExitTree();
    }

    private void OnDoorUnregistered()
    {
        EnableItem();
    }

    private void OnDoorRegistered()
    {
        DisableItem();

        if (IsInstanceValid(Editor) && Editor.itemObject == this)
        {
            Editor.ClearEditorItem();
        }
    }
}
