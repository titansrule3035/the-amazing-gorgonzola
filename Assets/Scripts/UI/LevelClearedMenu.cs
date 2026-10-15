using Godot;
using System;

public partial class LevelClearedMenu : Panel
{
    // UI nodes
    Button nextButton;
    Button quitButton;

    // State
    private bool lastVisible;

    /// <summary>
    /// True once Quit has been pressed. LocalGameManager checks this so the level-complete
    /// fade doesn't also start loading the next level while we load the main menu.
    /// Reset every time the menu is shown, so it can never stay stuck from an earlier visit.
    /// </summary>
    public bool IsQuitting { get; private set; }

    private static LevelClearedMenu instance;

    public override void _Ready()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            QueueFree();
            return;
        }

        IsQuitting = false;

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
                IsQuitting = false;
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
        IsQuitting = false;
        Color col = new Color(0, 0, 0, 1);
        FadePanel.GetInstance().FadeOut();
    }

    void QuitButtonPressed()
    {
        DisableButtons();
        IsQuitting = true;
        FadePanel fadePanel = FadePanel.GetInstance();
        GlobalGameManager.GetInstance().canPause = false;
        fadePanel.FadeOut();
        fadePanel.OnFadeOut += MainMenu;
    }

    void DisableButtons()
    {
        nextButton.Disabled = quitButton.Disabled = true;
    }

    void EnableButtons()
    {
        nextButton.Disabled = quitButton.Disabled = false;
    }

    public async void MainMenu()
    {
        await GlobalGameManager.GetInstance().LoadSceneLevel(0);
        FadePanel.GetInstance().OnFadeOut -= MainMenu;
        GlobalGameManager.GetInstance().gamePaused = false;
        IsQuitting = false;
    }

    public static LevelClearedMenu GetInstance()
    {
        return instance;
    }

    public override void _ExitTree()
    {
        if (instance == this)
        {
            instance = null;
        }

        base._ExitTree();
    }
}