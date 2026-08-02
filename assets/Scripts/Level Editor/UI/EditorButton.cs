using Godot;
using System;

public partial class EditorButton : ToolBarButton
{
    public override void _Ready()
    {
        menuButtons[4] = menu.GetNode<Button>("QuitButton/Button");

        menuButtons[4].Pressed += QuitButtonPressed;

        base._Ready();
    }
    void QuitButtonPressed()
    {
        blockMouse.MouseFilter = MouseFilterEnum.Stop;
        GetTree().CurrentScene.GetNode<QuitMenu>("CanvasLayer/UI/QuitMenu").ShowMenu();
    }
}
