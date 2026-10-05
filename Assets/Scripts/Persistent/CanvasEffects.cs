using Godot;
using System;

public partial class CanvasEffects : Control
{
    // Singleton instance
    private static CanvasEffects instance;

    // Events raised on fade operations
    public event Action<bool>? OnFadeOut;
    public event Action<bool>? OnFadeIn;
    public event Action? OnLevelCompleteFadeOut;

    // Default timings for fade in/out (editable in the inspector)
    [Export] public float fadeinTime = 0.5f;
    [Export] public float fadeoutTime = 1.5f;

    // Cached node references
    private ColorRect colorRect;
    private Tween tween;

    bool gorgKilled = false;

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
        colorRect = GetNode<ColorRect>("ColorRect");

        colorRect.Color = new Color(255, 255, 255, 0);

        colorRect.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (((Runtime)GetTree().CurrentScene).editorMode)
        {
            EditorGameManager.GetInstance()?.OnGorgFound += OnGorgFound;
        }
        else
        {
            GlobalGameManager.GetInstance()?.OnGorgFound += OnGorgFound;
        }
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
        colorRect.MouseFilter = MouseFilterEnum.Stop;

        colorRect.Visible = true;

        colorRect.Color = new Color(fadeToColor.R, fadeToColor.G, fadeToColor.B, 0);

        tween?.Kill();
        tween = CreateTween();
        tween.TweenProperty(colorRect, "color", fadeToColor, duration);
        tween.Finished += () =>
        {
            colorRect.MouseFilter = MouseFilterEnum.Ignore;

            OnFadeOut?.Invoke(gorgKilled);

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
        gorgKilled = false;
        colorRect.Visible = true;
        Color endResult = new Color(colorRect.Color.R, colorRect.Color.G, colorRect.Color.B, 0);

        tween?.Kill();
        tween = CreateTween();
        tween.TweenProperty(colorRect, "color", endResult, duration);
        tween.Finished += () =>
        {
            GlobalGameManager ggm = GlobalGameManager.GetInstance();
            if (ggm != null)
            {
                OnFadeIn?.Invoke(ggm.levelCompleted);
            }
            colorRect.Visible = false;
        };
    }

    private void OnGorgFound()
    {
        Action GorgKilled = () =>
        {
            gorgKilled = true;
        };
        GorgKilled += () =>
        {
            Gorgonzola.GetInstance().OnKilled -= GorgKilled;
        };

        Gorgonzola.GetInstance().OnKilled += GorgKilled;
    }

    public override void _ExitTree()
    {
        instance = null;
        base._ExitTree();
    }
}
