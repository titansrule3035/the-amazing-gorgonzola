using Godot;
using System;
using System.Linq;

public partial class MainMenu : Control
{
    public Button[] buttons;

    public override void _Ready()
    {
        buttons = new Button[4];
        buttons[0] = GetNode<Button>("CenterContainer/VBoxContainer/NewGame");
        buttons[0].Pressed += NewGameButtonPressed;
        buttons[1] = GetNode<Button>("CenterContainer/VBoxContainer/Options");
        buttons[1].Pressed += OptionsButtonPressed;
        buttons[2] = GetNode<Button>("CenterContainer2/VBoxContainer/LoadGame");
        buttons[2].Pressed += LoadGameButtonPressed;
        buttons[3] = GetNode<Button>("CenterContainer2/VBoxContainer/Quit");
        buttons[3].Pressed += QuitButtonPressed;

        CanvasEffects.GetInstance().OnFadeIn += OnFadeIn;
        CanvasEffects.GetInstance().OnFadeOut += OnFadeOut;

        GlobalGameManager.GetInstance().canPause = false;

        base._Ready();

    }

    void NewGameButtonPressed()
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
    void LoadGameButtonPressed()
    {

    }
    void QuitButtonPressed()
    {
        GetTree().Quit();
    }
    void OnFadeOut()
    {
        GlobalGameManager.GetInstance().LoadNextLevel();
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
