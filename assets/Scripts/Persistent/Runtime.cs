using Godot;
using System;

public partial class Runtime : Node2D
{
    [Export] public bool editorMode = false;
    [Export] public PackedScene editorScene;
    [Export] public PackedScene gameScene;

    Node2D gameContainer;

    Node2D editorContainer;
    public override void _Ready()
    {
        gameContainer = GetNode<Node2D>("game");

        editorContainer = GetNode<Node2D>("editor");

        if (editorMode)
        {
            SwitchToEditor(false);
        }
        else
        {
            SwitchToGame(false);
        }
    }

    public void SwitchToEditor(bool fade)
    {
        editorMode = true;

        if (fade)
        {
            CanvasEffects fadePanel = CanvasEffects.GetInstance();

            fadePanel.OnFadeOut += OnFadeOut;

            fadePanel.FadeOut(fadePanel.fadeoutTime, Colors.Black);
        }
        else
        {
            SwitchModes();
        }
    }

    public void SwitchToGame(bool fade)
    {
        editorMode = false;

        if (fade)
        {
            CanvasEffects fadePanel = CanvasEffects.GetInstance();

            fadePanel.OnFadeOut += OnFadeOut;

            fadePanel.FadeOut(fadePanel.fadeoutTime, Colors.Black);
        }
        else
        {
            SwitchModes();
        }
    }

    private async void OnFadeOut(bool gorgKilled)
    {
        SwitchModes();

        CanvasEffects fadePanel = CanvasEffects.GetInstance();

        fadePanel.FadeIn(fadePanel.fadeinTime);

        CanvasEffects.GetInstance().OnFadeOut -= OnFadeOut;
    }

    private async void SwitchModes()
    {
        Node2D mode;
        Node2D oldMode = editorMode ? gameContainer : editorContainer;
        Node2D target = editorMode ? editorContainer : gameContainer;

        foreach (Node child in oldMode.GetChildren())
        {
            child.QueueFree();
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        mode = editorMode ? editorScene.Instantiate<Node2D>() : gameScene.Instantiate<Node2D>();

        if (editorMode)
        {
            DisplayServer.WindowSetSize(new(1728, 864));
            GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        }
        else
        {
            DisplayServer.WindowSetSize(new(1248, 832));
            GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
        }

        target.AddChild(mode);
    }
}
