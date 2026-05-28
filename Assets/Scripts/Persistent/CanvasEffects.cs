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
    private Sprite2D sprite;
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
        sprite = GetNode<Sprite2D>("Sprite2D");

        // Start fully clear (transparent)
        sprite.Modulate = new Color(sprite.Modulate.R, sprite.Modulate.G, sprite.Modulate.B, 0);
    }

    public void FadeIn(float duration)
    {
        // Fade from opaque to transparent
        Color endResult = new Color(sprite.Modulate.R, sprite.Modulate.G, sprite.Modulate.B, 0);

        tween?.Kill(); // cancel previous tweens if any
        tween = CreateTween();
        tween.TweenProperty(sprite, "modulate", endResult, duration);
        tween.Finished += () =>
        {
            OnFadeIn?.Invoke(GlobalGameManager.GetInstance().levelCompleted);

            // Optionally: OnFadeCompleted?.Invoke(levelPassed);
        };
    }

    public void FadeIn()
    {
        // Fade from opaque to transparent
        Color endResult = new Color(sprite.Modulate.R, sprite.Modulate.G, sprite.Modulate.B, 0);

        tween?.Kill(); // cancel previous tweens if any
        tween = CreateTween();
        tween.TweenProperty(sprite, "modulate", endResult, fadeinTime);
        tween.Finished += () =>
        {
            OnFadeIn?.Invoke(GlobalGameManager.GetInstance().levelCompleted);
            // Optionally: OnFadeCompleted?.Invoke(levelPassed);
        };
    }

    public void FadeOut(float duration, Color fadeToColor)
    {
        // Start transparent
        sprite.Modulate = new Color(fadeToColor.R, fadeToColor.G, fadeToColor.B, 0);

        tween?.Kill(); // cancel previous tweens if any
        tween = CreateTween();
        tween.TweenProperty(sprite, "modulate", fadeToColor, duration);
        tween.Finished += () =>
        {
            OnFadeOut?.Invoke();
            if (GlobalGameManager.GetInstance().levelCompleted)
            {
                OnLevelCompleteFadeOut?.Invoke();
            }
        };
    }

    public void FadeOut(Color fadeToColor)
    {
        // Start transparent
        sprite.Modulate = new Color(fadeToColor.R, fadeToColor.G, fadeToColor.B, 0);

        tween?.Kill(); // cancel previous tweens if any
        tween = CreateTween();
        tween.TweenProperty(sprite, "modulate", fadeToColor, fadeoutTime);
        tween.Finished += () =>
        {
            OnFadeOut?.Invoke();
            if (GlobalGameManager.GetInstance().levelCompleted)
            {
                OnLevelCompleteFadeOut?.Invoke();
            }
        };
    }

    public static CanvasEffects GetInstance()
    {
        return instance;
    }
}
