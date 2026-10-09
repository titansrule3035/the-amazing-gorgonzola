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
        FadePanel.GetInstance().OnFadeIn += OnFadeIn;

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

        DisableButtonsState(false);

        base._Ready();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Input.IsActionJustReleased("F5"))
        {
            for (int i = 0; i < GlobalGameManager.GetInstance().GetLevelCount(); i++)
            {
                if (GlobalGameManager.GetInstance().levelGroups[i].HasTAGLEVELs())
                {
                    for (int j = 0; j < GlobalGameManager.GetInstance().levelGroups[i].GetTAGLEVELCount(); j++)
                    {
                        ((Runtime)GetTree().CurrentScene).ExportLevel(GlobalGameManager.GetInstance().levelGroups[i].Scene.Instantiate(), GlobalGameManager.GetInstance().levelGroups[i].GetTAGLEVELName(j));
                    }
                }
            }
        }
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
        if (Input.IsActionPressed("ctrl"))
        {
            ((Runtime)GetTree().CurrentScene).SwitchToEditor(true);

            DisableButtonsState(true);
        }
        else
        {
            GetTree().Quit();
        }
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