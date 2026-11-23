using Godot;
using System;

public partial class QuitMenu : Control
{
    public bool programMode = false;
    [Export] Button quitButton;
    [Export] public Button cancelButton;
    public FileButton fileButton;
    LevelEditorMain main;
    bool quit;

    public override void _Ready()
    {
        quitButton.Disabled = cancelButton.Disabled = false;

        quitButton.Pressed += QuitEditor;

        cancelButton.Pressed += CancelButtonPressed;
        fileButton = GetTree().CurrentScene.GetNode<FileButton>("editor/main/CanvasLayer/UI/ToolBar/FileButton");
        main = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main");
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
        bool consoleOpen = main.IsInputRestrictedBy(DebugConsole.GetInstance());
        if (Visible)
        {
            if (quit)
            {
                if (!consoleOpen && Input.IsActionPressed("ctrl"))
                {
                    if (!consoleOpen && Input.IsActionJustPressed("q"))
                    {
                        QuitEditor();
                    }
                }
            }
            if (!consoleOpen && Input.IsActionJustReleased("q") && Visible)
            {
                quit = true;
            }
            if (!consoleOpen && Input.IsActionJustPressed("escape"))
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
        main.SetInputRestriction(this, state, state);
    }

    void QuitEditor()
    {
        if (!programMode)
        {
            quitButton.Disabled = cancelButton.Disabled = true;
            SetMenuVisibility(false);

            Runtime.GetInstance().SwitchToGame(true);
        }
        else
        {
            GetTree().Quit();
            programMode = false;
        }
    }
}
