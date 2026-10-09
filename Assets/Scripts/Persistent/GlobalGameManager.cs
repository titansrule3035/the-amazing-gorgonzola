using Godot;
using GodotPlugins.Game;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public partial class GlobalGameManager : Node2D
{
    private static GlobalGameManager instance;

    [Export] public int activeLevelGroupIndex = 0;
    [Export] public int activeTagLevelIndex = -1;
    [Export] public Godot.Collections.Array<LevelGroup> levelGroups = new();
    private readonly List<LevelGroup> levels = new();

    private bool levelTransitioning;
    private Node2D activeLevel;
    public LocalGameManager localGM;
    private readonly HashSet<object> pauseLocks = new();
    public bool pauseLocked => pauseLocks.Count == 0;

    public event Action OnFirstFrame;
    public event Action OnLevelLoaded;
    public event Action OnFlush;
    public event Action? OnGorgFound;
    public event Action? OnGorgUnregistered;

    public Gorgonzola gorgonzola;
    public bool levelCompleted = false;
    public bool gamePaused = false;
    public bool canPause = true;
    public bool canMove = true;

    public int completedWorlds = 0;
    public int deaths = 0;
    public int clonesKilled = 0;
    public List<string> collectibles = new();

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
        foreach (var levelGroup in levelGroups)
        {
            if (levelGroup != null)
            {
                levels.Add(levelGroup);
            }
        }

        if (levels.Count == 0)
        {
            GD.PrintErr("No level groups assigned! Drag them into the LevelGroups array in the Inspector.");
            return;
        }

        StartLevelSequence();

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
        }

        if (!pauseLocked)
        {
            if (Input.IsActionJustPressed("pause") && canPause && canMove)
            {
                gamePaused = GetTree().Paused = !gamePaused;
            }
        }

        PauseMenu.GetInstance().Visible = GetTree().Paused = gamePaused;

        base._Process(delta);
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

    void ResetInstance()
    {
        instance = this;
    }

    public Vector2I LoadLevelFromSaveFile()
    {
        SaveData saveData = SaveManager.LoadGame();
        int savedLevelGroupIndex = saveData?.activeLevelGroupIndex ?? 0;
        int savedTagLevelIndex = saveData?.activeTagLevelIndex ?? -1;
        // this should load the group, then the taglevel index from the save file
        return new Vector2I(savedLevelGroupIndex, savedTagLevelIndex);
    }

    /// <summary>
    /// Deloads the current level, resets per-level state, and instantiates the group's scene.
    /// Every level load (new group, next tag level, reload) goes through here.
    /// </summary>
    public async Task LoadSceneLevel(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= levelGroups.Count)
        {
            GD.PrintErr($"[LEVEL] Invalid group index: {groupIndex}");
            return;
        }

        LevelGroup group = levelGroups[groupIndex];

        if (group == null || group.Scene == null)
        {
            GD.PrintErr($"[LEVEL] Group {groupIndex} has no scene.");
            return;
        }

        await DeloadLevel();

        Node currentScene = GetTree().CurrentScene;

        if (!GodotObject.IsInstanceValid(currentScene))
        {
            GD.PrintErr("[LEVEL] Current scene is no longer valid.");
            return;
        }

        Node2D levelParent = currentScene.GetNodeOrNull<Node2D>("game/main");

        if (levelParent == null)
        {
            GD.PrintErr("[LEVEL] Could not find game/main.");
            return;
        }

        activeLevelGroupIndex = groupIndex;
        levelCompleted = false;
        Node2D newLevel = group.Scene.Instantiate<Node2D>();
        activeLevel = newLevel;
        levelParent.CallDeferred("add_child", newLevel);
        await ToSignal(newLevel, Node.SignalName.Ready);

        OnLevelLoaded?.Invoke();
    }

    public async Task LoadTagLevel(int groupIndex, int tagLevelIndex)
    {
        if (groupIndex < 0 || groupIndex >= levelGroups.Count || levelGroups[groupIndex] == null)
        {
            GD.PrintErr($"[LEVEL] Invalid group index for TAG level: {groupIndex}");
            return;
        }

        LevelGroup group = levelGroups[groupIndex];

        if (tagLevelIndex < 0 || tagLevelIndex >= group.TagLevels.Count)
        {
            GD.PrintErr($"[LEVEL] Invalid TAG level index {tagLevelIndex} for group {groupIndex}.");
            return;
        }

        string tagLevelPath = group.TagLevels[tagLevelIndex];

        if (string.IsNullOrEmpty(tagLevelPath))
        {
            GD.PrintErr($"[LEVEL] TAG level {tagLevelIndex} in group {groupIndex} is empty.");
            return;
        }

        using Godot.FileAccess file = Godot.FileAccess.Open(tagLevelPath, Godot.FileAccess.ModeFlags.Read);

        if (file == null)
        {
            GD.PrintErr($"[LEVEL] Could not open TAG level file '{tagLevelPath}': {Godot.FileAccess.GetOpenError()}");
            return;
        }

        string levelText = file.GetAsText();

        await ((Runtime)GetTree().CurrentScene).ImportLevel(activeLevel, LevelData.Decode(levelText));
    }

    /// <summary>
    /// Loads whatever activeLevelGroupIndex / activeTagLevelIndex currently point at:
    /// deload old level -> instantiate group scene -> import tag level (if the group has any).
    /// </summary>
    private async Task LoadCurrentLevel()
    {
        if (activeLevelGroupIndex < 0 ||
            activeLevelGroupIndex >= levelGroups.Count)
        {
            GD.PrintErr($"[LEVEL] Invalid group index: {activeLevelGroupIndex}");
            return;
        }

        LevelGroup group = levelGroups[activeLevelGroupIndex];

        if (group == null)
        {
            GD.PrintErr($"[LEVEL] Level group {activeLevelGroupIndex} is null.");
            return;
        }

        await LoadSceneLevel(activeLevelGroupIndex);

        if (!GodotObject.IsInstanceValid(activeLevel))
            return;

        if (group.HasTAGLEVELs())
        {
            if (activeTagLevelIndex < 0)
            {
                activeTagLevelIndex = 0;
            }

            await LoadTagLevel(activeLevelGroupIndex, activeTagLevelIndex);
        }
    }

    private void StartLevelSequence()
    {
        activeLevelGroupIndex = 0;
        activeTagLevelIndex = -1;

        //TIL that using `_ =` can be used to suppress the warning for an unawaited async call, as you discard the returned Task
        _ = LoadCurrentLevel();
    }

    public async void LoadNextLevel()
    {
        if (levelTransitioning)
        {
            return;
        }

        if (activeLevelGroupIndex < 0 || activeLevelGroupIndex >= levelGroups.Count)
        {
            GD.PrintErr($"[LEVEL] Invalid group index: {activeLevelGroupIndex}");
            return;
        }

        levelTransitioning = true;

        try
        {
            LevelGroup group = levelGroups[activeLevelGroupIndex];

            if (group != null && group.HasTAGLEVELs() && activeTagLevelIndex + 1 < group.TagLevels.Count)
            {
                // Next TAG level in the same group. The scene still gets fully deloaded and
                // reloaded by LoadCurrentLevel, even if it's the same scene as before.
                activeTagLevelIndex++;
            }
            else
            {

                if (!AdvanceToNextGroup())
                {
                    return;
                }
            }

            await LoadCurrentLevel();
        }
        catch (Exception e)
        {
            GD.PrintErr($"[LEVEL] LoadNextLevel failed: {e}");
        }
        finally
        {
            levelTransitioning = false;
        }
    }

    /// <summary>
    /// Moves the indices to the first level of the next group.
    /// Returns false (and leaves the indices alone) if there are no more groups.
    /// </summary>
    private bool AdvanceToNextGroup()
    {

        if (activeLevelGroupIndex + 1 >= levelGroups.Count)
        {
            return false;
        }

        activeLevelGroupIndex++;
        activeTagLevelIndex = -1;

        return true;
    }

    public async void ReloadLevel()
    {
        if (levelTransitioning)
        {
            return;
        }

        levelTransitioning = true;

        try
        {
            await LoadCurrentLevel();
        }
        catch (Exception e)
        {
            GD.PrintErr($"[LEVEL] Reload failed: {e}");
        }
        finally
        {
            levelTransitioning = false;
        }
    }

    private async Task DeloadLevel()
    {
        Node2D oldLevel = activeLevel;

        if (activeLevelGroupIndex >= 0 &&
            activeLevelGroupIndex < levelGroups.Count &&
            levelGroups[activeLevelGroupIndex] != null &&
            levelGroups[activeLevelGroupIndex].HasTAGLEVELs())
        {
            SaveManager.SaveGame(this);
        }

        // Clear references before the old level starts exiting.
        activeLevel = null;
        gorgonzola = null;
        localGM = null;

        if (!GodotObject.IsInstanceValid(oldLevel))
        {
            return;
        }

        // If the level is inside the tree, wait for its actual exit.
        if (oldLevel.IsInsideTree())
        {
            oldLevel.QueueFree();

            await ToSignal(oldLevel, Node.SignalName.TreeExited);
        }
        else
        {
            // It was instantiated but never added to the tree.
            oldLevel.Free();
        }
    }

    public void ShowVictoryMenu(bool condition)
    {
        LevelClearedMenu.GetInstance().Visible = condition;
    }

    public bool IsLastLevel()
    {
        return activeLevelGroupIndex == levelGroups.Count - 1;
    }

    public int GetLevelCount()
    {
        return levels.Count;
    }

    public Node2D GetActiveLevel()
    {
        return activeLevel;
    }

    private async System.Threading.Tasks.Task WaitForGameLoaded()
    {
        while (Gorgonzola.GetInstance() == null)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        gorgonzola = Gorgonzola.GetInstance();
        OnFirstFrame?.Invoke();
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

    // Helper to strip trailing numbers from object names
    private static string StripTrailingNumber(string name)
    {
        int i = name.Length;
        while (i > 0 && char.IsDigit(name[i - 1]))
            i--;
        return name[..i];
    }

    public void RegisterGorg(Gorgonzola gorgonzola)
    {
        this.gorgonzola = gorgonzola;
        OnGorgFound?.Invoke();
    }

    public void UnregisterGorg()
    {
        gorgonzola = null;
        OnGorgUnregistered?.Invoke();
    }
}