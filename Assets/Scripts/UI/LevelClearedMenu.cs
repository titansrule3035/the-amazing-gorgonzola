using Godot;
using System;

public partial class LevelClearedMenu : Panel
{
    // UI nodes
    Button nextButton;
    Button quitButton;

    // State
    private bool lastVisible;

    // Singleton
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
                EnableButtons();
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
        DisableButtons();
        Color col = new Color(0, 0, 0, 1);
        CanvasEffects.GetInstance().FadeOut(col);
    }

    void QuitButtonPressed()
    {
        DisableButtons();
        CanvasEffects canvas = CanvasEffects.GetInstance();
        GlobalGameManager.GetInstance().canPause = false;
        canvas.FadeOut(Colors.Black);
        canvas.OnFadeOut += MainMenu;
    }

    void DisableButtons()
    {
        nextButton.Disabled = quitButton.Disabled = true;
    }

    void EnableButtons()
    {
        nextButton.Disabled = quitButton.Disabled = false;
    }

    public void MainMenu(bool throwaway)
    {
        // every signal connected to fade out needs a flag, I don't have a use for this bool, but I need to have it in the signature to match the signal
        GlobalGameManager.GetInstance().LoadLevel(0);
        CanvasEffects.GetInstance().OnFadeOut -= MainMenu;
        GlobalGameManager.GetInstance().gamePaused = false;
    }

    public static LevelClearedMenu GetInstance()
    {
        return instance;
    }

    public override void _ExitTree()
    {
        instance = null;

        base._ExitTree();
    }
}