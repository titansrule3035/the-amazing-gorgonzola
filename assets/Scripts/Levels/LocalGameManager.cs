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

        FadePanel fadePanel = FadePanel.GetInstance();
        KillPanel killPanel = KillPanel.GetInstance();

        // FadePanel: level complete, quitting, pausing. Each panel gets its OWN fade-out handler
        // so the handler never has to guess which panel just went opaque.
        fadePanel.OnFadeIn += OnFadeIn;
        fadePanel.OnFadeOut += OnFadePanelFadeOut;
        fadePanel.OnLevelCompleteFadeOut += OnLevelCompleted;

        // KillPanel: the player died
        killPanel.OnFadeIn += OnFadeIn;
        killPanel.OnFadeOut += OnKillPanelFadeOut;

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

    /// <summary>
    /// FadePanel has finished fading out (level complete, quit, pause menu, etc.).
    /// Clears out clones and reveals the screen again. Whatever loads the next scene is
    /// driven separately (OnLevelCompleted, or the menu that started the fade).
    /// </summary>
    protected void OnFadePanelFadeOut()
    {
        OnFlush?.Invoke();
        FadePanel.GetInstance().FadeIn();
    }

    /// <summary>
    /// KillPanel has finished fading out (the main player died).
    /// Reloads the level (unless it can't or shouldn't be) and fades the red panel back out.
    /// The red panel is ALWAYS the one faded back in, so it can never get stuck on screen.
    /// </summary>
    protected void OnKillPanelFadeOut()
    {
        OnFlush?.Invoke();

        GlobalGameManager ggm = GlobalGameManager.GetInstance();
        if (!ggm.levelCompleted && !ggm.gamePaused && !ggm.IsLastLevel())
        {
            ggm.ReloadLevel();
        }

        KillPanel.GetInstance().FadeIn();
    }

    protected async void OnLevelCompleted()
    {
        if (LevelClearedMenu.GetInstance()?.IsQuitting == true)
        {
            return;
        }

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
        BasePlayerController.MainPlayerKilled -= OnMainPlayerKilled;
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
        if (instance != this)
        {
            base._ExitTree();
            return;
        }

        instance = null;

        FadePanel fadePanel = FadePanel.GetInstance();
        if (fadePanel != null)
        {
            fadePanel.OnFadeIn -= OnFadeIn;
            fadePanel.OnFadeOut -= OnFadePanelFadeOut;
            fadePanel.OnLevelCompleteFadeOut -= OnLevelCompleted;
        }

        KillPanel killPanel = KillPanel.GetInstance();
        if (killPanel != null)
        {
            killPanel.OnFadeIn -= OnFadeIn;
            killPanel.OnFadeOut -= OnKillPanelFadeOut;
        }

        SpawnGorg gorgSpawnPoint = SpawnGorg.GetInstance();
        if (gorgSpawnPoint != null)
        {
            gorgSpawnPoint.GorgSpawned -= OnGorgFound;
        }

        BasePlayerController.MainPlayerKilled -= OnMainPlayerKilled;

        GlobalGameManager.GetInstance()?.UnregisterLGM();

        base._ExitTree();
    }
}