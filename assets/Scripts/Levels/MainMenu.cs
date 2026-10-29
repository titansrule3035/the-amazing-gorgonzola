using Godot;
using System;
using System.Linq;
using System.Text.Json;

public partial class MainMenu : Control
{
    // Node references used by the main menu
    public TextureButton[] buttons;

    [Export] public OptionsMenu optionsMenu;
    [Export] public CreditsMenu creditsMenu;

    // Lifecycle 
    public override void _Ready()
    {
        // Cache button nodes and hook up their events
        buttons = new TextureButton[5];
        buttons[0] = GetNode<TextureButton>("center_container/v_box_container/play");
        buttons[0].Pressed += PlayButtonPressed;
        buttons[1] = GetNode<TextureButton>("center_container/v_box_container/options");
        buttons[1].Pressed += OptionsButtonPressed;
        buttons[2] = GetNode<TextureButton>("center_container/v_box_container/quit");
        buttons[2].Pressed += QuitButtonPressed;
        buttons[3] = GetNode<TextureButton>("center_container2/v_box_container/level_editor");
        buttons[3].Pressed += EditorButtonPressed;
        buttons[4] = GetNode<TextureButton>("credits");
        buttons[4].Pressed += CreditsButtonPressed;

        // Subscribe to canvas fade events
        FadePanel.GetInstance().OnFadeIn += OnFadeIn;

        // Disable pausing while in the main menu
        GlobalGameManager.GetInstance().canPause = false;

        // Subscribe to back buttons
        optionsMenu.backButtonPressed += () =>
        {
            ShowMenu();
            optionsMenu.HideMenu();
        };

        creditsMenu.backButtonPressed += () =>
        {
            ShowMenu();
            creditsMenu.HideMenu();
        };

        optionsMenu.HideMenu();
        creditsMenu.HideMenu();
        ShowMenu();

        DisableButtonsState(false);

        base._Ready();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    public override void _ExitTree()
    {
        // Unsubscribe from events to avoid dangling references
        FadePanel.GetInstance().OnFadeIn -= OnFadeIn;
        FadePanel.GetInstance().OnFadeOut -= OnFadeOut;
        GlobalGameManager.GetInstance().canPause = true;
        base._ExitTree();
    }

    // Button handlers
    void PlayButtonPressed()
    {
        DisableButtonsState(true);
        Color col = new Color(0, 0, 0, 1);
        FadePanel.GetInstance().OnFadeOut += OnFadeOut;
        FadePanel.GetInstance().FadeOut();
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

    void EditorButtonPressed()
    {
        DisableButtonsState(true);
        ((Runtime)GetTree().CurrentScene).SwitchToEditor(true);
    }

    void CreditsButtonPressed()
    {
        HideMenu();
        creditsMenu.ShowMenu();
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
        FadePanel.GetInstance().OnFadeOut -= OnFadeOut;

        GlobalGameManager ggm = GlobalGameManager.GetInstance();
        int levelGroupIndex = SaveManager.LoadCompletedLevelGroups();
        int tagLevelIndex = SaveManager.LoadCompletedTagLevels();

        GlobalGameManager.GetInstance().LoadNextLevel();
        FadePanel.GetInstance().FadeIn();
    }

    void OnFadeIn()
    {
        // Ensure the game is unpaused after fade in and stop listening to this event here
        GlobalGameManager.GetInstance().gamePaused = false;
        FadePanel.GetInstance().OnFadeIn -= OnFadeIn;
    }

}