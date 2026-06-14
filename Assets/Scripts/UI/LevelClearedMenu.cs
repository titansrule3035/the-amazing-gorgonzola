using Godot;
using System;

public partial class LevelClearedMenu : Panel
{
    Button nextButton;
    Button quitButton;

    private bool lastVisible;

    private static LevelClearedMenu instance;

    public override void _Ready()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            QueueFree();
        }
        nextButton = GetNode<Button>("Button1");
        quitButton = GetNode<Button>("Button2");

        nextButton.Pressed += NextButtonPressed;
        quitButton.Pressed += QuitButtonPressed;
        
        base._Ready();
    }


    public override void _Process(double delta)
    {
        if (Visible != lastVisible)
        {
            var ggm = GlobalGameManager.GetInstance();

            if (Visible)
            {
                ggm.AddPauseLock(this);
            }
            else
            {
                ggm.RemovePauseLock(this);
            }

            lastVisible = Visible;
        }
    }

    void NextButtonPressed()
    {
        Color col = new Color(0, 0, 0, 1);
        CanvasEffects.GetInstance().FadeOut(col);
    }

    void QuitButtonPressed()
    {
        nextButton.Disabled = quitButton.Disabled = false;
        CanvasEffects canvas = CanvasEffects.GetInstance();
        GlobalGameManager.GetInstance().canPause = false;
        canvas.FadeOut(Colors.Black);
        canvas.OnFadeOut += MainMenu;
    }

    public void MainMenu()
    {
        GlobalGameManager.GetInstance().LoadLevel(0);
        CanvasEffects.GetInstance().OnFadeOut -= MainMenu;
        GlobalGameManager.GetInstance().gamePaused = false;
    }

    public static LevelClearedMenu GetInstance()
    {
        return instance;
    }
}