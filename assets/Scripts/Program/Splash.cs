using Godot;
using System;
using System.Text;

public partial class Splash : Node2D
{
    [Export] PackedScene RuntimeScene;
    [Export] float foregroundFadeSec = 1.2f; // how long the fade to opaque takes

    bool canFade = true;
    bool fading = false;

    AnimationPlayer animationPlayer;

    Sprite2D fadePanelSprite;
    Gradient fadePanelGradient;
    Tween fadeTween;

    public override void _Ready()
    {
        animationPlayer = GetNode<AnimationPlayer>("animation_player");

        fadePanelSprite = GetNode<Sprite2D>("fade_panel");
        SetupForegroundGradient();
    }

    // Grabs the gradient from the foreground's texture and duplicates it so
    // changing alpha at runtime never touches a resource shared with other scenes.
    void SetupForegroundGradient()
    {
        switch (fadePanelSprite.Texture)
        {
            case GradientTexture1D tex1D:
                fadePanelGradient = (Gradient)tex1D.Gradient.Duplicate();
                tex1D.Gradient = fadePanelGradient;
                break;
            case GradientTexture2D tex2D:
                fadePanelGradient = (Gradient)tex2D.Gradient.Duplicate();
                tex2D.Gradient = fadePanelGradient;
                break;
            default:
                break;
        }
    }

    void SetForegroundAlpha(float alpha)
    {
        if (fadePanelGradient == null)
        {
            return;
        }

        Color c = fadePanelGradient.GetColor(0);
        c.A = alpha;
        fadePanelGradient.SetColor(0, c);
    }

    // onFinished runs once the fade reaches full opacity (can be null).
    public void StartForegroundFade(Action onFinished = null)
    {
        if (!fading)
        {
            fading = true;

            canFade = false;

            float startAlpha = fadePanelGradient != null ? fadePanelGradient.GetColor(0).A : 0f;

            fadeTween?.Kill();
            fadeTween = CreateTween();
            fadeTween.TweenMethod(Callable.From<float>(SetForegroundAlpha), startAlpha, 1.0f, foregroundFadeSec);

            if (onFinished != null)
            {
                fadeTween.TweenCallback(Callable.From(onFinished));
            }
        }
    }

    public void UpdateCanFade(bool state)
    {
        if (!fading)
        {
            canFade = state;
        }
    }

    public void EndSplash()
    {
        Runtime instance = RuntimeScene.Instantiate<Runtime>();
        instance.ranWithSplash = true;

        if (IsInstanceValid(this))
        {
            GetTree().ChangeSceneToNode(instance);
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Detects any keyboard key, mouse click, gamepad button, or screen touch
        if (@event.IsPressed() && !@event.IsEcho())
        {
            if (canFade && !fading)
            {
                animationPlayer.Pause();

                StartForegroundFade(EndSplash);
            }
        }
    }
}