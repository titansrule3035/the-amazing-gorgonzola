using Godot;
using System;

public partial class PauseMenu : Panel
{
    Button resumeButton;
    Button quitButton;

    private static PauseMenu instance;

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
        resumeButton.Disabled = quitButton.Disabled = false;
        CanvasEffects canvas = CanvasEffects.GetInstance();
        GlobalGameManager.GetInstance().canPause = false;
        canvas.FadeOut(Colors.Black);
        canvas.OnFadeOut += MainMenu;
    }

    public static PauseMenu GetInstance()
    {
        return instance;
    }

    public void MainMenu()
    {
        GlobalGameManager ggm = GlobalGameManager.GetInstance();
        ggm.LoadLevel(0);
        CanvasEffects.GetInstance().OnFadeOut -= MainMenu;
        ggm.gamePaused = false;
    }
}
