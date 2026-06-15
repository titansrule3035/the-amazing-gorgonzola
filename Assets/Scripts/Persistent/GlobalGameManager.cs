using Godot;
using System;
using System.Collections.Generic;

public partial class GlobalGameManager : Node2D
{
    private static GlobalGameManager instance;

    [Export] public int activeLevelIndex = 0;
    [Export] public Godot.Collections.Array<PackedScene> levelScenes = new();

    private Node2D activeLevel;
    public LocalGameManager localGM;
    private readonly List<PackedScene> levels = new();

    private readonly HashSet<object> pauseLocks = new();
    public bool pauseLocked => pauseLocks.Count == 0;

    public event Action OnFirstFrame;
    public event Action OnLevelLoaded;
    public event Action OnFlush;

    public Gorgonzola gorgonzola;
    public bool levelCompleted = false;
    public bool gamePaused = false;
    public bool canPause = true;
    public bool canMove = true;

    public int completedWorlds = 0;
    public int deaths = 0;
    public int clonesKilled = 0;
    public List<string> collectibles = new();

    [Export] public Texture2D[] mouseIcons;
    private int currentCursorFrame = 0;
    private bool isCursorAnimating = false;
    private double cursorTimer = 0.0;
    [Export] public float FrameDuration = 0.06f;

    public override async void _Ready()
    {
        if (instance != null)
        {
            GD.Print("More than one GlobalGameManager exists! Deleting this one...");
            QueueFree();
            return;
        }

        instance = this;

        levels.Clear();
        foreach (var scene in levelScenes)
        {
            if (scene != null)
            {
                levels.Add(scene);
            }
        }

        if (levels.Count == 0)
        {
            GD.PrintErr("No level scenes assigned! Drag them into the LevelScenes array in the Inspector.");
            return;
        }

        LoadLevel(0);
        await WaitForGameLoaded();

        SaveData saveData = SaveManager.LoadGame();

        completedWorlds = saveData?.completedWorlds ?? 0;
        deaths = saveData?.deaths ?? 0;
        clonesKilled = saveData?.clonesKilled ?? 0;
        collectibles = saveData?.collectibles ?? new();

    }

    public override void _Process(double delta)
    {
        if (!levelCompleted)
        {
            LevelClearedMenu.GetInstance().Visible = false;
            if (Input.IsActionJustPressed("reset") && Gorgonzola.GetInstance() != null && canMove)
            {
                Gorgonzola.GetInstance().CallDeferred("Kill");
            }
        }

        if (!pauseLocked)
        {
            if (Input.IsActionJustPressed("pause") && canPause && canMove)
            {
                gamePaused = GetTree().Paused = !gamePaused;
            }
        }
        Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);

        PauseMenu.GetInstance().Visible = GetTree().Paused = gamePaused;
        base._Process(delta);

        if (isCursorAnimating)
        {
            cursorTimer += delta;

            if (cursorTimer >= FrameDuration)
            {
                cursorTimer -= FrameDuration;
                currentCursorFrame++;

                if (currentCursorFrame >= mouseIcons.Length)
                {
                    currentCursorFrame = 0;
                    isCursorAnimating = false;
                }
            }
        }

        if (mouseIcons.Length > 0 && mouseIcons[currentCursorFrame] != null)
        {
            Input.SetCustomMouseCursor(mouseIcons[currentCursorFrame]);
        }

    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb &&
            mb.ButtonIndex == MouseButton.Left &&
            mb.Pressed &&
            !isCursorAnimating)
        {
            isCursorAnimating = true;
            currentCursorFrame = 0;
            cursorTimer = 0.0;
        }
    }

    public override void _ExitTree()
    {
        if (instance == this)
            instance = null;

        base._ExitTree();
    }

    public static GlobalGameManager GetInstance()
    {
        return instance;
    }

    private async System.Threading.Tasks.Task WaitForGameLoaded()
    {
        while (Gorgonzola.GetInstance() == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        gorgonzola = Gorgonzola.GetInstance();
        OnFirstFrame?.Invoke();
    }

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

    private void InstantiateActiveLevel()
    {
        activeLevel = levels[activeLevelIndex].Instantiate<Node2D>();
        GetTree().Root.CallDeferred("add_child", activeLevel);

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

    public void LoadLevelFromSaveFile()
    {
        SaveData saveData = SaveManager.LoadGame();
        int savedLevelIndex = saveData?.activeLevelIndex ?? 0;
        LoadLevel(savedLevelIndex);
    }

    public void LoadNextLevel()
    {
        DeloadLevel();

        activeLevelIndex++;

        InstantiateActiveLevel();
    }

    public void ReloadLevel()
    {
        DeloadLevel();
        InstantiateActiveLevel();
    }

    private void DeloadLevel()
    {
        if (activeLevelIndex != 0)
        {
            SaveManager.SaveGame(this);
        }

        gorgonzola = null;

        if (activeLevel != null && activeLevel.IsInsideTree())
        {
            activeLevel.QueueFree();
        }

        activeLevel = null;

        levelCompleted = false;
    }


    public void ShowVictoryMenu(bool condition)
    {
        LevelClearedMenu.GetInstance().Visible = condition;
    }

    public bool IsLastLevel()
    {
        return activeLevelIndex == levels.Count - 1;
    }

    public int GetLevelCount()
    {
        return levels.Count;
    }

    public Node2D GetActiveLevel()
    {
        return activeLevel;
    }
    public void RegisterLGM(LocalGameManager lgm, bool allowPausing)
    {
        localGM = lgm;
        AddPauseLock(localGM);
    }
    public void UnregisterLGM()
    {
        RemovePauseLock(localGM);
        localGM = null;
    }

    public void AddPauseLock(object owner)
    {
        pauseLocks.Add(owner);
    }

    public void RemovePauseLock(object owner)
    {
        pauseLocks.Remove(owner);
    }
}