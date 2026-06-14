using Godot;
using System;
using System.Linq;

public partial class MainMenu : Control
{
    public TextureButton[] buttons;

    public override void _Ready()
    {
        buttons = new TextureButton[3];
        buttons[0] = GetNode<TextureButton>("CenterContainer/VBoxContainer/Play");
        buttons[0].Pressed += PlayButtonPressed;
        buttons[1] = GetNode<TextureButton>("CenterContainer/VBoxContainer/Options");
        buttons[1].Pressed += OptionsButtonPressed;
        buttons[2] = GetNode<TextureButton>("CenterContainer/VBoxContainer/Quit");
        buttons[2].Pressed += QuitButtonPressed;

        CanvasEffects.GetInstance().OnFadeIn += OnFadeIn;
        CanvasEffects.GetInstance().OnFadeOut += OnFadeOut;

        GlobalGameManager.GetInstance().canPause = false;

        base._Ready();

    }

    void PlayButtonPressed()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Disabled = true;
        }
        Color col = new Color(0, 0, 0, 1);
        CanvasEffects.GetInstance().FadeOut(col);
    }
    void OptionsButtonPressed()
    {

    }
    void QuitButtonPressed()
    {
        GetTree().Quit();
    }
    void OnFadeOut()
    {
        GlobalGameManager ggm = GlobalGameManager.GetInstance();
        int levelIndex = SaveManager.LoadCompletedLevels();

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
        GlobalGameManager.GetInstance().gamePaused = false;
        CanvasEffects.GetInstance().OnFadeIn -= OnFadeIn;
    }

    public override void _ExitTree()
    {
        CanvasEffects.GetInstance().OnFadeIn -= OnFadeIn;
        CanvasEffects.GetInstance().OnFadeOut -= OnFadeOut;
        GlobalGameManager.GetInstance().canPause = true;
        base._ExitTree();
    }
}
