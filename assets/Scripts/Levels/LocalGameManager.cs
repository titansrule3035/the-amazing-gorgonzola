using Godot;
using System;

public abstract partial class LocalGameManager : Node2D
{
    private static LocalGameManager instance;

    [Export] public Vector2 levelOrigin;

    public event Action OnFlush;

    [Export] bool allowPausing = true;

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

        FadePanel.GetInstance().OnFadeIn += HandleFadeIn;
        FadePanel.GetInstance().OnFadeOut += HandleFadeOut;
        FadePanel.GetInstance().OnLevelCompleteFadeOut += HandleLevelCompleted;
        KillPanel.GetInstance().OnFadeOut += HandleFadeOut;
        KillPanel.GetInstance().OnFadeIn += HandleFadeIn;

        if (SpawnGorg.GetInstance() != null)
        {
            SpawnGorg.GetInstance().GorgSpawned += HandleGorgSpawned;
        }

        GlobalGameManager.GetInstance().RegisterLGM(this, allowPausing);

        GlobalGameManager.GetInstance().canMove = false;
    }

    public override void _Process(double delta)
    {
        if (flush)
        {
            OnFlush?.Invoke();

            KillPanel.GetInstance().FadeOut();
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
            KillPanel.GetInstance().FadeIn();
            return;
        }
        FadePanel.GetInstance().FadeIn();
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

    private void HandleFadeIn()
    {
        GlobalGameManager.GetInstance().canMove = true;
    }


    private void HandleGorgSpawned(Gorgonzola gorg)
    {
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

        var fade = FadePanel.GetInstance();
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

        GlobalGameManager.GetInstance().UnregisterLGM();

        base._ExitTree();
    }
}
