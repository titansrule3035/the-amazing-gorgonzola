using Godot;
using System;

public partial class FadePanel : Control
{
    // Singleton instance
    private static FadePanel instance;

    // Events raised on fade operations
    public event Action? OnFadeOut;
    public event Action? OnFadeIn;
    public event Action? OnLevelCompleteFadeOut;

    // Default timings for fade in/out (editable in the inspector)
    [Export] public float fadeinTime = 0.5f;
    [Export] public float fadeoutTime = 1.5f;

    // True while a fade tween is running (read-only from outside)
    public bool IsFading { get; private set; } = false;

    // Cached node references
    private ColorRect colorRect;
    private Tween tween;

    // Lifecycle
    public override void _Ready()
    {
        Show();

        if (instance != null)
        {
            DebugManager.Log($"More than one instance of FadePanel was found in the scene! Deleting this one...", "FadePanel");
            QueueFree();
            return;
        }

        instance = this;
        colorRect = GetNode<ColorRect>("ColorRect");

        colorRect.Color = new Color(0, 0, 0, 1);

        colorRect.Visible = false;

        SetOpaque();
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
    public void FadeOut(float duration)
    {
        StartFadingOut(duration);
    }

    // Public method to fade out using the exported default duration
    public void FadeOut()
    {
        StartFadingOut(fadeoutTime);
    }

    // Instantly makes the screen fully black (opaque), cancelling any fade in progress
    public void SetOpaque()
    {
        tween?.Kill();
        IsFading = false;

        colorRect.Visible = true;
        colorRect.Color = new Color(0, 0, 0, 1);
        colorRect.MouseFilter = MouseFilterEnum.Ignore; // matches the end state of FadeOut()
    }

    // Instantly makes the screen fully clear, cancelling any fade in progress
    public void SetClear()
    {
        tween?.Kill();
        IsFading = false;

        colorRect.Color = new Color(colorRect.Color.R, colorRect.Color.G, colorRect.Color.B, 0);
        colorRect.MouseFilter = MouseFilterEnum.Ignore;
        colorRect.Visible = false;
    }

    public static FadePanel GetInstance()
    {
        return instance;
    }

    // Internal helpers
    // Starts the fade out tween and invokes the appropriate events when finished.
    private void StartFadingOut(float duration)
    {
        colorRect.MouseFilter = MouseFilterEnum.Stop;

        colorRect.Visible = true;

        Color fadeToColor = new Color(0, 0, 0, 1);
        colorRect.Color = new Color(fadeToColor.R, fadeToColor.G, fadeToColor.B, 0);

        tween?.Kill();
        IsFading = true;
        tween = CreateTween();
        tween.TweenProperty(colorRect, "color", fadeToColor, duration);
        tween.Finished += () =>
        {
            // Clear the flag before raising events so listeners can start a new fade
            IsFading = false;

            colorRect.MouseFilter = MouseFilterEnum.Ignore;

            OnFadeOut?.Invoke();

            GlobalGameManager? ggm = GlobalGameManager.GetInstance();
            if (ggm != null)
            {
                if (ggm.levelCompleted)
                {
                    OnLevelCompleteFadeOut?.Invoke();
                }
            }
        };
    }

    // Starts the fade in tween and invokes the appropriate events when finished.
    private void StartFadingIn(float duration)
    {
        colorRect.Visible = true;
        Color endResult = new Color(colorRect.Color.R, colorRect.Color.G, colorRect.Color.B, 0);

        tween?.Kill();
        IsFading = true;
        tween = CreateTween();
        tween.TweenProperty(colorRect, "color", endResult, duration);
        tween.Finished += () =>
        {
            // Clear the flag before raising events so listeners can start a new fade
            IsFading = false;

            GlobalGameManager ggm = GlobalGameManager.GetInstance();
            if (ggm != null)
            {
                OnFadeIn?.Invoke();
            }
            colorRect.Visible = false;
        };
    }

    public override void _ExitTree()
    {
        // A duplicate that freed itself in _Ready must not clear the real singleton
        if (instance == this)
        {
            instance = null;
        }

        base._ExitTree();
    }
}