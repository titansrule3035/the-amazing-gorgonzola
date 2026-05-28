using Godot;
using System;

public partial class PauseMenu : Panel
{
    Button nextButton;
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
        base._Ready();
        nextButton = GetNode<Button>("Button1");
        quitButton = GetNode<Button>("Button2");

        nextButton.Pressed += NextButtonPressed;
        quitButton.Pressed += QuitButtonPressed;
    }

    void NextButtonPressed()
    {
        GlobalGameManager.GetInstance().gamePaused = false;
        Engine.TimeScale = 1;
    }

    void QuitButtonPressed()
    {
        CanvasEffects.GetInstance().FadeOut(Colors.Black);
        CanvasEffects.GetInstance().OnFadeOut += MainMenu;
    }

    public static PauseMenu GetInstance()
    {
        return instance;
    }

    public void MainMenu()
    {
        GlobalGameManager.GetInstance().LoadLevel(0);
        CanvasEffects.GetInstance().OnFadeOut -= MainMenu;
        GlobalGameManager.GetInstance().gamePaused = false;
    }
}
