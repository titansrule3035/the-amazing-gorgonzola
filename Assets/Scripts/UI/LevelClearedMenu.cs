using Godot;
using System;

public partial class LevelClearedMenu : Panel
{
    Button nextButton;
    Button quitButton;

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
            base._Ready();
        nextButton = GetNode<Button>("Button1");
        quitButton = GetNode<Button>("Button2");

        nextButton.Pressed += NextButtonPressed;
        quitButton.Pressed += QuitButtonPressed;
    }

    void NextButtonPressed()
    {
        Color col = new Color(0, 0, 0, 1);
        CanvasEffects.GetInstance().FadeOut(1.5f, col, true);
    }

    void QuitButtonPressed()
    {
        GetTree().Quit();
    }

    public static LevelClearedMenu GetInstance()
    {
        return instance;
    }
}