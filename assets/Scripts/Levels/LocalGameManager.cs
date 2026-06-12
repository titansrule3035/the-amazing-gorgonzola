using Godot;
using System;

public abstract partial class LocalGameManager : Node2D
{
    private static LocalGameManager instance;

    [Export] public Vector2 levelOrigin;

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
        CanvasEffects.GetInstance().OnLevelCompleteFadeOut += HandleLevelCompleted;

        if (SpawnGorg.GetInstance() != null)
        {
            SpawnGorg.GetInstance().GorgSpawned += HandleGorgSpawned;
        }

        GlobalGameManager.GetInstance().localGM = this;

        GlobalGameManager.GetInstance().canMove = false;
    }

    public override void _Process(double delta)
    {
        if (flush)
        {
            OnFlush?.Invoke();

            // Start fade out only once
            CanvasEffects.GetInstance().FadeOut(dieColor);
            flush = false;
        }
    }

    protected void HandleFadeOut()
    {
        OnFlush?.Invoke();
        if (!GlobalGameManager.GetInstance().levelCompleted && !GlobalGameManager.GetInstance().gamePaused)
        {
            if (!GlobalGameManager.GetInstance().IsLastLevel())
            {
                GlobalGameManager.GetInstance()?.ReloadLevel();
            }
        }
        CanvasEffects.GetInstance().FadeIn();
    }

    protected void HandleLevelCompleted()
    {
        if (!GlobalGameManager.GetInstance().IsLastLevel())
        {
            GlobalGameManager.GetInstance().LoadNextLevel();
        }
        else
        {
            GlobalGameManager.GetInstance().LoadLevel(0);
        }
    }

    private void HandleFadeIn(bool levelPassed)
    {
        GlobalGameManager.GetInstance().canMove = true;
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

        var fade = CanvasEffects.GetInstance();
        if (fade != null)
        {
            fade.OnFadeIn -= HandleFadeIn;
            fade.OnFadeOut -= HandleFadeOut;
            fade.OnLevelCompleteFadeOut -= HandleLevelCompleted;
        }

        var gorgSpawnPoint = SpawnGorg.GetInstance();
        if (gorgSpawnPoint != null)
        {
            SpawnGorg.GetInstance().GorgSpawned -= HandleGorgSpawned;
        }

        GlobalGameManager.GetInstance().localGM = null;

        base._ExitTree();
    }
}
