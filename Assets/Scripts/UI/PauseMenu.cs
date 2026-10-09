using Godot;
using System;

public partial class PauseMenu : Panel
{
    // UI nodes
    Button resumeButton;
    Button quitButton;

    // Singleton
    private static PauseMenu instance;

    /// <summary>
    /// Initialize the pause menu and wire button callbacks.
    /// </summary>
    public override void _Ready()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            QueueFree();
        }

        resumeButton = GetNode<Button>("Button1");
        quitButton = GetNode<Button>("Button2");

        resumeButton.Pressed += ResumeButtonPressed;
        quitButton.Pressed += QuitButtonPressed;

        base._Ready();
    }

    void ResumeButtonPressed()
    {
        GlobalGameManager.GetInstance().gamePaused = false;
        Engine.TimeScale = 1;
    }

    void QuitButtonPressed()
    {
        resumeButton.Disabled = quitButton.Disabled = true;
        FadePanel canvas = FadePanel.GetInstance();
        GlobalGameManager.GetInstance().canPause = false;
        canvas.FadeOut();
        canvas.OnFadeOut += MainMenu;
    }

    public static PauseMenu GetInstance()
    {
        return instance;
    }

    public async void MainMenu()
    {
        GlobalGameManager ggm = GlobalGameManager.GetInstance();
        await ggm.LoadSceneLevel(0);
        FadePanel.GetInstance().OnFadeOut -= MainMenu;
        ggm.gamePaused = false;
    }

    public override void _ExitTree()
    {
        instance = null;

        base._ExitTree();
    }
}
