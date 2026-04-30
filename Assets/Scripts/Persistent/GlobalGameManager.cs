using Godot;
using System;
using System.Collections.Generic;

public partial class GlobalGameManager : Node2D
{
    private static GlobalGameManager instance;

    [Export] public int activeLevelIndex;

    private Node2D activeLevel;                 // instantiated level root
    public LocalGameManager localGM;            // per-level manager (read/write relationship)
    private readonly List<PackedScene> levels = new();

    // === EVENTS ===
    public event Action OnFirstFrame;
    public event Action OnLevelLoaded;
    public event Action OnFlush;

    public Gorgonzola gorgonzola;

    public bool levelCompleted = false;

    public bool gamePaused = false;

    // ------------------------------------------------------------
    //  LIFECYCLE
    // ------------------------------------------------------------

    public override async void _Ready()
    {
        if (instance != null)
        {
            GD.Print("More than one GlobalGameManager exists! Deleting this one...");
            QueueFree();
            return;
        }

        instance = this;

        LoadOrderedLevelScenes("res://Assets/Scenes/World/Level");

        if (levels.Count == 0)
        {
            GD.PrintErr("No level scenes found! GlobalGameManager cannot load anything.");
            return;
        }

        LoadLevel(0);

        await WaitForGameLoaded();
    }

    public static GlobalGameManager GetInstance()
    {
        return instance;
    }

    public override void _ExitTree()
    {
        if (instance == this)
            instance = null;

        base._ExitTree();
    }

    // ------------------------------------------------------------
    //  GAME READY WAIT
    // ------------------------------------------------------------

    private async System.Threading.Tasks.Task WaitForGameLoaded()
    {
        while (Gorgonzola.GetInstance() == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        gorgonzola = Gorgonzola.GetInstance();
        OnFirstFrame?.Invoke();
    }

    // ------------------------------------------------------------
    //  LEVEL LOADING
    // ------------------------------------------------------------

    private void LoadOrderedLevelScenes(string directoryPath)
    {
        levels.Clear();

        var dir = DirAccess.Open(directoryPath);
        if (dir == null)
        {
            GD.PrintErr($"Cannot open directory: {directoryPath}");
            return;
        }

        dir.ListDirBegin();
        var sceneFiles = new List<(int index, string path)>();

        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (fileName == "." || fileName == "..")
            {
                fileName = dir.GetNext();
                continue;
            }

            if (!dir.CurrentIsDir() && fileName.EndsWith(".tscn"))
            {
                string nameWithoutExtension = fileName.Replace(".tscn", "");

                if (int.TryParse(nameWithoutExtension, out int levelNumber))
                {
                    string scenePath = $"{directoryPath}/{fileName}";
                    sceneFiles.Add((levelNumber, scenePath));
                }
                else
                {
                    GD.PrintErr($"Skipping file with invalid number: {fileName}");
                }
            }

            fileName = dir.GetNext();
        }

        dir.ListDirEnd();

        sceneFiles.Sort((a, b) => a.index.CompareTo(b.index));

        foreach (var (_, path) in sceneFiles)
        {
            PackedScene scene = GD.Load<PackedScene>(path);
            if (scene != null)
                levels.Add(scene);
            else
                GD.PrintErr($"Failed to load scene at: {path}");
        }
    }

    // Shared core instantiation routine
    private void InstantiateActiveLevel()
    {
        activeLevel = levels[activeLevelIndex].Instantiate<Node2D>();
        GetTree().Root.CallDeferred("add_child", activeLevel);

        // LocalGameManager handshake (your read/write relationship)
        localGM = activeLevel.GetNodeOrNull<LocalGameManager>("LocalGameManager");

        if (localGM != null)
            activeLevel.GlobalPosition = localGM.levelOrigin;

        OnLevelLoaded?.Invoke();
    }

    public void LoadLevel(int levelIndex)
    {
        DeloadLevel();

        activeLevelIndex = levelIndex;
        InstantiateActiveLevel();
    }

    public void LoadNextLevel()
    {
        DeloadLevel();

        activeLevelIndex = (activeLevelIndex + 1) % levels.Count;

        InstantiateActiveLevel();
    }

    public void ReloadLevel()
    {
        DeloadLevel();
        InstantiateActiveLevel();
    }

    // ------------------------------------------------------------
    //  DELoad
    // ------------------------------------------------------------

    private void DeloadLevel()
    {
        // Clear gameplay references safely
        gorgonzola = null;

        if (activeLevel != null && activeLevel.IsInsideTree())
        {
            activeLevel.QueueFree();
        }

        activeLevel = null;
        localGM = null;

        levelCompleted = false;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!levelCompleted)
        {
            LevelClearedMenu.GetInstance().Visible = false;
        }

        if (Input.IsActionJustPressed("pause"))
        {
            gamePaused = !gamePaused;
            Engine.TimeScale = Engine.TimeScale > 0 ? Engine.TimeScale = 0 : Engine.TimeScale = 1;
        }
        PauseMenu.GetInstance().Visible = gamePaused;
    }
    public void ShowVictoryMenu(bool condition)
    {
        LevelClearedMenu.GetInstance().Visible |= condition;
    }
}
