using Godot;
using System;

public partial class QuitMenu : Control
{
    public bool programMode = false;
    [Export] Button quitButton;
    [Export] public Button cancelButton;
    public FileButton fileButton;
    bool quit;

    public override void _Ready()
    {
        quitButton.Disabled = cancelButton.Disabled = false;

        quitButton.Pressed += QuitEditor;

        cancelButton.Pressed += CancelButtonPressed;
        fileButton = GetTree().CurrentScene.GetNode<FileButton>("editor/main/CanvasLayer/UI/ToolBar/FileButton");
    }

    private void CancelButtonPressed()
    {
        HideMenu();
        quit = false;

        if (GetParent() is Ui ui)
        {
            ui.blockMouse.MouseFilter = MouseFilterEnum.Ignore;
            fileButton.UpdateMenuAndButton(false);
        }

        programMode = false;
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
                        QuitEditor();
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

            Ui ui = GetParent() as Ui;

            ui.blockMouse.MouseFilter = MouseFilterEnum.Stop;
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

    void QuitEditor()
    {
        if (!programMode)
        {
            quitButton.Disabled = cancelButton.Disabled = true;

            ((Runtime)GetTree().CurrentScene).SwitchToGame(true);
        }
        else
        {
            GetTree().Quit();
            programMode = false;
        }
    }
}
