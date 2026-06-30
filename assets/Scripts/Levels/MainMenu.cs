using Godot;
using System;
using System.Linq;
using System.Text.Json;

public partial class MainMenu : Control
{
    // Node references used by the main menu
    public TextureButton[] buttons;
    [Export] public OptionsMenu optionsMenu;

    // Lifecycle 
    public override void _Ready()
    {
        // Cache button nodes and hook up their events
        buttons = new TextureButton[3];
        buttons[0] = GetNode<TextureButton>("center_container/v_box_container/play");
        buttons[0].Pressed += PlayButtonPressed;
        buttons[1] = GetNode<TextureButton>("center_container/v_box_container/options");
        buttons[1].Pressed += OptionsButtonPressed;
        buttons[2] = GetNode<TextureButton>("center_container/v_box_container/quit");
        buttons[2].Pressed += QuitButtonPressed;

        // Subscribe to canvas fade events
        CanvasEffects.GetInstance().OnFadeIn += OnFadeIn;
        CanvasEffects.GetInstance().OnFadeOut += OnFadeOut;

        // Disable pausing while in the main menu
        GlobalGameManager.GetInstance().canPause = false;


        // Subscribe to options' back button
        optionsMenu.backButtonPressed += () => 
        {
            ShowMenu();
            optionsMenu.HideMenu();
        };

        optionsMenu.HideMenu();
        ShowMenu();

        base._Ready();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    public override void _ExitTree()
    {
        // Unsubscribe from events to avoid dangling references
        CanvasEffects.GetInstance().OnFadeIn -= OnFadeIn;
        CanvasEffects.GetInstance().OnFadeOut -= OnFadeOut;
        GlobalGameManager.GetInstance().canPause = true;
        base._ExitTree();
    }

    // Button handlers
    void PlayButtonPressed()
    {
        Color col = new Color(0, 0, 0, 1);
        CanvasEffects.GetInstance().FadeOut(col);
    }

    void OptionsButtonPressed()
    {
        HideMenu();
        optionsMenu.ShowMenu();
    }

    void QuitButtonPressed()
    {
        GetTree().Quit();
    }

    // Prevent further interaction while transitioning
    bool DisableButtonsState(bool state)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Disabled = state;
        }
        return state;
    }

    // Menu controls
    void ShowMenu()
    {
        this.Visible = !DisableButtonsState(false);
    }

    void HideMenu()
    {
        this.Visible = !DisableButtonsState(true);
    }

    // Fade callbacks
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

}