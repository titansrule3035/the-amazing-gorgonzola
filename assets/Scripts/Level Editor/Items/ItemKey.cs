using Godot;
using System;

public partial class ItemKey : EditorItemObject
{
    private EditorGameManager gm;

    public override void _Ready()
    {
        base._Ready();
        gm = GetTree().CurrentScene.GetNode<EditorGameManager>("editor/main/editor_game_manager");
        gm.OnKeyRegistered += OnKeyRegistered;
        gm.OnKeyUnregistered += OnKeyUnregistered;

        if (gm.key != null)
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
        gm.OnKeyRegistered -= OnKeyRegistered;
        gm.OnKeyUnregistered -= OnKeyUnregistered;

        base._ExitTree();
    }

    private void OnKeyUnregistered()
    {
        EnableItem();
    }

    private void OnKeyRegistered()
    {
        DisableItem();

        if (IsInstanceValid(Editor) && Editor.itemObject == this)
        {
            Editor.ClearEditorItem();
        }
    }
}
