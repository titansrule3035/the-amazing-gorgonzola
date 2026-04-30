using Godot;
using System;

public abstract partial class LocalGameManager : Node2D
{
    private static LocalGameManager instance;

    [Export] public Vector2 levelOrigin;

    // === FADE TIMING (IN SECONDS) ===
    [Export] public float fadeinTime = 0.5f;
    [Export] public float fadeoutTime = 1.5f;

    // === EVENTS ===
    public event Action OnFlush;

    // === FADE COLORS ===
    private Color dieColor = new Color(96f / 255f, 0f, 0f, 1f);

    private bool flush;

    public override void _Ready()
    {
        if (instance != null)
        {
            GD.Print("More than one LocalGameManager exists! Deleting this one...");
            QueueFree();
            return;
        }

        instance = this;

        // Connect signals instead of direct calls
        CanvasEffects.GetInstance().OnFadeIn += HandleFadeIn;
        CanvasEffects.GetInstance().OnFadeOut += HandleFadeOut;

        if(SpawnGorg.GetInstance() != null)
        {
            SpawnGorg.GetInstance().GorgSpawned += HandleGorgSpawned;
        }
    }

    public override void _Process(double delta)
    {
        if (flush)
        {
            OnFlush?.Invoke();

            // Start fade out only once
            CanvasEffects.GetInstance().FadeOut(fadeoutTime, dieColor, false);
            flush = false;
        }
        else
        {
            if (!GlobalGameManager.GetInstance().levelCompleted)
            {
                if (Input.IsActionJustPressed("reset") && Gorgonzola.GetInstance() != null)
                {
                    Gorgonzola.GetInstance().CallDeferred("Kill");
                }
            }
        }
    }

    protected void HandleFadeOut(bool levelPassed)
    {
        OnFlush?.Invoke();
        CanvasEffects.GetInstance().FadeIn(fadeinTime, levelPassed);
        if(GlobalGameManager.GetInstance().activeLevelIndex != 2)
        {
            if (!levelPassed)
            {
                // GlobalGameManager is responsible for reload/transition
                GlobalGameManager.GetInstance()?.ReloadLevel();
            }
            else
            {
                GlobalGameManager.GetInstance().LoadNextLevel();
            }
        }
        else
        {
            GlobalGameManager.GetInstance().LoadLevel(0);
        }
    }

    private void HandleFadeIn(bool levelPassed)
    {
        ProcessFade(levelPassed);
    }


    private void HandleGorgSpawned(Gorgonzola gorg)
    {
        // Subscribe Gorgonzola’s kill event
        gorg.OnKilled += HandleGorgKilled;
    }

    private void HandleGorgKilled()
    {
        flush = true;
    }

    private void ProcessFade(bool levelPassed)
    {
        // Optional global logic after fade completes
    }

    public static LocalGameManager GetInstance()
    {
        return instance;
    }

    public override void _ExitTree()
    {
        if (instance == this)
        {
            instance = null;
        }

        // Disconnect signals safely
        var fade = CanvasEffects.GetInstance();
        if (fade != null)
        {
            fade.OnFadeIn -= HandleFadeIn;
            fade.OnFadeOut -= HandleFadeOut;
        }

        var gorgSpawnPoint = SpawnGorg.GetInstance();
        if (gorgSpawnPoint != null)
        {
            SpawnGorg.GetInstance().GorgSpawned -= HandleGorgSpawned;
        }

        base._ExitTree();
    }
}
