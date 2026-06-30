using Godot;
using System;

public partial class QuitMenu : Control
{
    [Export] Button quitButton;
    [Export] public Button cancelButton;
    public FileButton fileButton;
    bool quit;

    public override void _Ready()
    {
        quitButton.Pressed += () =>
        {
            GetTree().Quit();
        };

        cancelButton.Pressed += CancelButtonPressed;
        fileButton = GetTree().CurrentScene.GetNode<FileButton>("CanvasLayer/UI/ToolBar/FileButton");
    }

    private void CancelButtonPressed()
    {
        HideMenu();
        quit = false;

        if(GetParent() is Ui ui)
        {
            ui.blockMouse.MouseFilter = MouseFilterEnum.Ignore;
            fileButton.UpdateMenuAndButton(false);
        }
    }

    public override void _Process(double delta)
    {
        if (Visible)
        {
            if (quit)
            {
                if (Input.IsActionPressed("ctrl"))
                {
                    if (Input.IsActionJustPressed("q"))
                    {
                        GetTree().Quit();
                    }
                }
            }
            if (Input.IsActionJustReleased("q") && Visible)
            {
                quit = true;
            }
            if (Input.IsActionJustPressed("escape"))
            {
                CancelButtonPressed();
            }
        }
    }

    public void ShowMenu()
    {
        SetMenuVisibility(true);
    }

    public void HideMenu()
    {
        SetMenuVisibility(false);
    }

    public void SetMenuVisibility(bool state)
    {
        quitButton.Disabled = cancelButton.Disabled = !state;
        Visible = state;
    }
}
