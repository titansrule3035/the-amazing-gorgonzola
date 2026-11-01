using Godot;
using System;

public partial class ItemKey : EditorItemObject
{
    private LevelEditorMain main;

    public override void _Ready()
    {
        base._Ready();
        main = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main");
        main.OnKeyRegistered += OnKeyRegistered;
        main.OnKeyUnregistered += OnKeyUnregistered;

        if (main.key != null)
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
        main.OnKeyRegistered -= OnKeyRegistered;
        main.OnKeyUnregistered -= OnKeyUnregistered;

        base._ExitTree();
    }

    private void OnKeyUnregistered()
    {
        EnableItem();
    }

    private void OnKeyRegistered()
    {
        DisableItem();
    }
}
