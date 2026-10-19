using Godot;
using System;

public partial class ItemDoor : EditorItemObject
{
    private LevelEditorMain main;

    public override void _Ready()
    {
        base._Ready();
        main = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main");
        main.OnDoorRegistered += OnDoorRegistered;
        main.OnDoorUnregistered += OnDoorUnregistered;

        if (main.door != null)
        {
            DisableItem();
        }
        else
        {
            EnableItem();
        }
    }

    public override void _ExitTree()
    {
        main.OnDoorRegistered -= OnDoorRegistered;
        main.OnDoorUnregistered -= OnDoorUnregistered;

        base._ExitTree();
    }

    private void OnDoorUnregistered()
    {
        EnableItem();
    }

    private void OnDoorRegistered()
    {
        DisableItem();
    }
}
