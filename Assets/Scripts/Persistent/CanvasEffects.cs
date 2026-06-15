using Godot;
using System;

public partial class CanvasEffects : Node2D
{
    // Singleton instance
    private static CanvasEffects instance;

    // Events raised on fade operations
    public event Action? OnFadeOut;
    public event Action<bool>? OnFadeIn;
    public event Action? OnLevelCompleteFadeOut;

    // Default timings for fade in/out (editable in the inspector)
    [Export] public float fadeinTime = 0.5f;
    [Export] public float fadeoutTime = 1.5f;

    // Cached node references
    private ColorRect colorRect;
    private Tween tween;

    // Lifecycle
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

        Color rectColor = colorRect.Color;
        colorRect.Color = new Color(rectColor.R, rectColor.G, rectColor.B, 0);

        colorRect.Visible = false;
    }

    // Public API
    // Public method to fade in with explicit duration
    public void FadeIn(float duration)
    {
        StartFadingIn(duration);
    }

    // Public method to fade in using the exported default duration
    public void FadeIn()
    {
        StartFadingIn(fadeinTime);
    }

    // Public method to fade out with explicit duration and color
    public void FadeOut(float duration, Color fadeToColor)
    {
        StartFadingOut(duration, fadeToColor);
    }

    // Public method to fade out using the exported default duration
    public void FadeOut(Color fadeToColor)
    {
        StartFadingOut(fadeoutTime, fadeToColor);
    }

    public static CanvasEffects GetInstance()
    {
        return instance;
    }

    // Internal helpers
    // Starts the fade out tween and invokes the appropriate events when finished.
    private void StartFadingOut(float duration, Color fadeToColor)
    {
        colorRect.Visible = true;

        colorRect.Color = new Color(fadeToColor.R, fadeToColor.G, fadeToColor.B, 0);

        tween?.Kill();
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

    // Starts the fade in tween and invokes the appropriate events when finished.
    private void StartFadingIn(float duration)
    {
        colorRect.Visible = true;
        Color endResult = new Color(colorRect.Color.R, colorRect.Color.G, colorRect.Color.B, 0);

        tween?.Kill();
        tween = CreateTween();
        tween.TweenProperty(colorRect, "color", endResult, duration);
        tween.Finished += () =>
        {
            OnFadeIn?.Invoke(GlobalGameManager.GetInstance().levelCompleted);
            colorRect.Visible = false;
        };
    }
}
