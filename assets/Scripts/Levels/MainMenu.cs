using Godot;
using System;
using System.Linq;

public partial class MainMenu : Control
{
    // Node references used by the main menu
    #region Nodes
    public TextureButton[] buttons;
    #endregion

    // Lifecycle methods
    #region Lifecycle
    public override void _Ready()
    {
        // Cache button nodes and hook up their events
        buttons = new TextureButton[3];
        buttons[0] = GetNode<TextureButton>("CenterContainer/VBoxContainer/Play");
        buttons[0].Pressed += PlayButtonPressed;
        buttons[1] = GetNode<TextureButton>("CenterContainer/VBoxContainer/Options");
        buttons[1].Pressed += OptionsButtonPressed;
        buttons[2] = GetNode<TextureButton>("CenterContainer/VBoxContainer/Quit");
        buttons[2].Pressed += QuitButtonPressed;

        // Subscribe to canvas fade events
        CanvasEffects.GetInstance().OnFadeIn += OnFadeIn;
        CanvasEffects.GetInstance().OnFadeOut += OnFadeOut;

        // Disable pausing while in the main menu
        GlobalGameManager.GetInstance().canPause = false;

        base._Ready();
    }

    public override void _ExitTree()
    {
        // Unsubscribe from events to avoid dangling references
        CanvasEffects.GetInstance().OnFadeIn -= OnFadeIn;
        CanvasEffects.GetInstance().OnFadeOut -= OnFadeOut;
        GlobalGameManager.GetInstance().canPause = true;
        base._ExitTree();
    }
    #endregion

    // Button handlers
    #region Button Handlers
    void PlayButtonPressed()
    {
        // Prevent further interaction while transitioning
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Disabled = true;
        }
        Color col = new Color(0, 0, 0, 1);
        CanvasEffects.GetInstance().FadeOut(col);
    }

    void OptionsButtonPressed()
    {
        // Options not implemented yet
    }

    void QuitButtonPressed()
    {
        GetTree().Quit();
    }
    #endregion

    // Fade callbacks
    #region Fade Callbacks
    void OnFadeOut()
    {
        GlobalGameManager ggm = GlobalGameManager.GetInstance();
        int levelIndex = SaveManager.LoadCompletedLevels();

        // If we have progress that's not the first or last level, load from save
        if (levelIndex != 0 && levelIndex != ggm.GetLevelCount() - 1)
        {
            ggm.LoadLevelFromSaveFile();
        }
        else
        {
            GlobalGameManager.GetInstance().LoadNextLevel();
        }
        CanvasEffects.GetInstance().FadeIn();
    }

    void OnFadeIn(bool levelCompleted)
    {
        // Ensure the game is unpaused after fade in and stop listening to this event here
        GlobalGameManager.GetInstance().gamePaused = false;
        CanvasEffects.GetInstance().OnFadeIn -= OnFadeIn;
    }
    #endregion
}
