using Godot;
using System;

public partial class CanvasEffects : Node2D
{
    private static CanvasEffects instance;

    // Events for fade out (screen covered) and fade in (screen cleared)
    public event Action? OnFadeOut;
    public event Action<bool>? OnFadeIn;
    public event Action? OnLevelCompleteFadeOut;

    // === FADE TIMING (IN SECONDS) ===
    [Export] public float fadeinTime = 0.5f;
    [Export] public float fadeoutTime = 1.5f;

    // Optional: you can keep this if you want a unified event after each fade completes
    // public event Action<bool>? OnFadeCompleted;

    // == NODE RESOURCES ===
    private ColorRect colorRect;
    private Tween tween;

    public override void _Ready()
    {
        Show();

        if (instance != null)
        {
            GD.Print("More than one instance of FadePanel was found in the scene! Deleting this one...");
            QueueFree();
            return;
        }

        instance = this;
        colorRect = GetNode<ColorRect>("CanvasLayer/ColorRect");

        // Start fully clear (transparent)
        Color rectColor = colorRect.Color;
        colorRect.Color = new Color(rectColor.R, rectColor.G, rectColor.B, 0);

        colorRect.Visible = false; // hide until needed
    }

    public void FadeIn(float duration)
    {
        StartFadingIn(duration);
    }

    public void FadeIn()
    {
        StartFadingIn(fadeinTime);
    }

    public void FadeOut(float duration, Color fadeToColor)
    {
        StartFadingOut(duration, fadeToColor);
    }

    public void FadeOut(Color fadeToColor)
    {
        StartFadingOut(fadeoutTime, fadeToColor);
    }

    public static CanvasEffects GetInstance()
    {
        return instance;
    }

    private void StartFadingOut(float duration, Color fadeToColor)
    {
        colorRect.Visible = true; // ensure it's visible for the fade
        // Start transparent
        colorRect.Color = new Color(fadeToColor.R, fadeToColor.G, fadeToColor.B, 0);

        tween?.Kill(); // cancel previous tweens if any
        tween = CreateTween();
        tween.TweenProperty(colorRect, "color", fadeToColor, duration);
        tween.Finished += () =>
        {
            OnFadeOut?.Invoke();
            if (GlobalGameManager.GetInstance().levelCompleted)
            {
                OnLevelCompleteFadeOut?.Invoke();
            }
        };
    }
    private void StartFadingIn(float duration)
    {
        colorRect.Visible = true; // ensure it's visible for the fade
        // Fade from opaque to transparent
        Color endResult = new Color(colorRect.Color.R, colorRect.Color.G, colorRect.Color.B, 0);

        tween?.Kill(); // cancel previous tweens if any
        tween = CreateTween();
        tween.TweenProperty(colorRect, "color", endResult, duration);
        tween.Finished += () =>
        {
            OnFadeIn?.Invoke(GlobalGameManager.GetInstance().levelCompleted);
            colorRect.Visible = false; // hide after fade in completes
            // Optionally: OnFadeCompleted?.Invoke(levelPassed);
        };
    }
}
