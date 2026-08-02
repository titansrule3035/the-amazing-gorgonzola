using Godot;
using GodotPlugins.Game;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

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

    public Main editorMain;

    [Export] public bool editorMode = false;
    [Export] public PackedScene editorScene;

    public override async void _Ready()
    {
        if (editorMode)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().ChangeSceneToPacked(editorScene);
            return;
        }

        // change resolution to match editor requirements
        /*
         Window window = GetWindow();

        Vector2I newRes = new(1248, 832);

        window.Size = newRes;

        window.ContentScaleSize = newRes;

        window.Position = (DisplayServer.ScreenGetSize(window.CurrentScreen) - window.Size) / 2;
        */

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
    public void ExportLevel(Node levelRoot, string exportName)
    {
        LevelData data = new();

        foreach (Node2D tileMapLayer in levelRoot.GetNode<Node2D>("tiles").GetChildren())
        {
            if (tileMapLayer is not TileMapLayer layer)
                continue;

            LayerData layerData = new()
            {
                Name = layer.Name
            };

            foreach (Vector2I cell in layer.GetUsedCells())
            {
                Vector2I atlas = layer.GetCellAtlasCoords(cell);

                layerData.Tiles.Add(new TileData
                {
                    X = cell.X,
                    Y = cell.Y,
                    SourceId = layer.GetCellSourceId(cell),
                    AtlasX = atlas.X,
                    AtlasY = atlas.Y,
                    Alternative = layer.GetCellAlternativeTile(cell)
                });
            }

            data.Layers.Add(layerData);
        }

        Node2D assetsRoot = levelRoot.GetNode<Node2D>("level_assets");
        Node2D clear_conditions = assetsRoot.GetNodeOrNull<Node2D>("clear_conditions");
        Node2D clones = assetsRoot.GetNodeOrNull<Node2D>("clones");
        Node2D hazards = assetsRoot.GetNodeOrNull<Node2D>("hazards");
        Node2D on_off_assets = assetsRoot.GetNodeOrNull<Node2D>("on_off_assets");
        Node2D semi_solid_tiles = assetsRoot.GetNodeOrNull<Node2D>("semi_solid_tiles");

        if (clear_conditions != null)
        {
            foreach (Node2D level_essential in clear_conditions.GetChildren())
            {
                data.ClearConditions.Add(new ObjectData(level_essential.GetType().Name, level_essential.Name, new Vector2(level_essential.GlobalPosition.X, level_essential.GlobalPosition.Y)));
            }
        }

        if (clones != null)
        {
            foreach (Node2D clone in clones.GetChildren())
            {
                data.Clones.Add(new ObjectData(clone.GetType().Name.ToString(), clone.Name, new Vector2(clone.GlobalPosition.X, clone.GlobalPosition.Y)));
            }
        }

        if (on_off_assets != null)
        {
            foreach (Node2D on_off_asset in on_off_assets.GetChildren())
            {
                string asset_type = on_off_asset.GetType().Name;
                if (asset_type == "OnOffSwitchMaster")
                {
                    OnOffSwitchMaster switchMaster = on_off_asset as OnOffSwitchMaster;
                    data.OnOffs.OnOffSwitchMaster = new OnOffSwitchMasterData(asset_type, on_off_asset.Name, new Vector2(on_off_asset.GlobalPosition.X, on_off_asset.GlobalPosition.Y), switchMaster.opened);
                }
                else if (asset_type == "OnOffBlockSwitch")
                {
                    OnOffSwitch switchNormal = on_off_asset as OnOffSwitch;
                    data.OnOffs.OnOffSwitches.Add(new ObjectData(asset_type, on_off_asset.Name, new Vector2(on_off_asset.GlobalPosition.X, on_off_asset.GlobalPosition.Y)));
                }
                else if (StripTrailingNumber(on_off_asset.Name) == "green_on_off_block")
                {
                    data.OnOffs.OnOffBlocks.Add(new ObjectData("GreenOnOffBlock", on_off_asset.Name, new Vector2(on_off_asset.GlobalPosition.X, on_off_asset.GlobalPosition.Y)));
                }
                else if (StripTrailingNumber(on_off_asset.Name) == "red_on_off_block")
                {
                    data.OnOffs.OnOffBlocks.Add(new ObjectData("RedOnOffBlock", on_off_asset.Name, new Vector2(on_off_asset.GlobalPosition.X, on_off_asset.GlobalPosition.Y)));
                }
            }
        }

        if (hazards != null)
        {
            foreach (Node2D hazard in hazards.GetChildren())
            {
                data.Hazards.Add(new ObjectData(hazard.GetType().Name, hazard.Name, new Vector2(hazard.GlobalPosition.X, hazard.GlobalPosition.Y)));
            }
        }

        if (semi_solid_tiles != null)
        {
            foreach (Node2D semi_solid_tile in semi_solid_tiles.GetChildren())
            {
                data.SemiSolidTiles.Add(new SemiSolidTileData(semi_solid_tile.GetType().Name, semi_solid_tile.Name, new Vector2(semi_solid_tile.GlobalPosition.X, semi_solid_tile.GlobalPosition.Y), semi_solid_tile.Scale));
            }
        }

        string json = JsonSerializer.Serialize(data,
        new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true
        });

        string newFile = $"C:\\Users\\Princ\\source\\repos\\C#\\Godot Projects\\the-amazing-gorgonzola\\TAGLEVELs\\{exportName}.taglevel";

        if (!DirAccess.DirExistsAbsolute(Path.GetDirectoryName(newFile)))
        {
            DirAccess.MakeDirAbsolute(Path.GetDirectoryName(newFile));
        }

        using Godot.FileAccess file = Godot.FileAccess.Open(newFile, Godot.FileAccess.ModeFlags.Write);

        file.StoreString(SaveManager.Encode(json));
    }

    // Helper to strip trailing numbers from object names
    private static string StripTrailingNumber(string name)
    {
        int i = name.Length;
        while (i > 0 && char.IsDigit(name[i - 1]))
            i--;
        return name[..i];
    }

    // Game control
    // Set audio volume
    public void UpdateBusVolume(string busName, float linearVolume)
    {
        int busIndex = AudioServer.GetBusIndex(busName);

        float dbVolume = Mathf.LinearToDb(linearVolume);

        AudioServer.SetBusVolumeDb(busIndex, dbVolume);
    }

    public void RegisterGorg(Gorgonzola gorgonzola)
    {
        this.gorgonzola = gorgonzola;
        editorMain = GetTree().CurrentScene as Main;
        OnGorgFound?.Invoke();
    }

    public void UnregisterGorg()
    {
        gorgonzola = null;
        OnGorgUnregistered?.Invoke();
    }
}