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
            GD.PrintErr("Only one LocalGameManager allowed per scene, deleting this one...");
            QueueFree();
            return;
        }

        instance = this;

        FadePanel.GetInstance().OnFadeIn += OnFadeIn;
        FadePanel.GetInstance().OnFadeOut += OnFadeOut;
        FadePanel.GetInstance().OnLevelCompleteFadeOut += OnLevelCompleted;
        KillPanel.GetInstance().OnFadeOut += OnFadeOut;
        KillPanel.GetInstance().OnFadeIn += OnFadeIn;

        if (SpawnGorg.GetInstance() != null)
        {
            SpawnGorg.GetInstance().GorgSpawned += OnGorgFound;
        }

        GlobalGameManager.GetInstance().RegisterLGM(this, allowPausing);

        GlobalGameManager.GetInstance().canMove = false;
    }

    public override void _Process(double delta)
    {
        if (flush)
        {
            OnFlush?.Invoke();

            flush = false;
        }
    }

    protected void OnFadeOut()
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

    protected async void OnLevelCompleted()
    {
        GlobalGameManager ggm = GlobalGameManager.GetInstance();
        if (!ggm.IsLastLevel())
        {
            ggm.LoadNextLevel();
        }
        else
        {
            await ggm.LoadSceneLevel(0);
        }
    }

    private void OnFadeIn()
    {
        GlobalGameManager.GetInstance().canMove = true;
    }


    private void OnGorgFound(Gorgonzola gorg)
    {
        BasePlayerController.MainPlayerKilled += OnMainPlayerKilled;
    }

    private void OnMainPlayerKilled()
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
            fade.OnFadeIn -= OnFadeIn;
            fade.OnFadeOut -= OnFadeOut;
            fade.OnLevelCompleteFadeOut -= OnLevelCompleted;
        }

        var gorgSpawnPoint = SpawnGorg.GetInstance();
        if (gorgSpawnPoint != null)
        {
            SpawnGorg.GetInstance().GorgSpawned -= OnGorgFound;
        }

        GlobalGameManager.GetInstance().UnregisterLGM();

        base._ExitTree();
    }
}
