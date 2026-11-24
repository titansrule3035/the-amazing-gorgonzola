using Godot;
using System;

public partial class EditorButton : ToolBarButton
{
    public override void _Ready()
    {
        base._Ready();

        menuButtons[4] = menu.GetNode<Button>("QuitButton/Button");

        menuButtons[4].Pressed += QuitButtonPressed;
    }
    void QuitButtonPressed()
    {
        GetTree().CurrentScene.GetNode<QuitMenu>("editor/main/CanvasLayer/UI/QuitMenu").ShowMenu();
    }
}
