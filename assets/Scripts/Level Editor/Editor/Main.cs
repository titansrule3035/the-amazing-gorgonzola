using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using TheAmazingGorgonzola.assets.Scripts.Level_Assets;
using static SemiSolidTileData;

public partial class Main : Node2D
{
    LevelData level;

    public Ui ui;

    public string filePath;

    public FileButton fileButton;

    public Gorgonzola gorgonzola;

    public ScrollButtonMenusController scrollButton;

    static Main instance;

    public Door door = null;
    public Action? OnDoorRegistered;
    public Action? OnDoorUnregistered;

    public Key key = null;
    public Action? OnKeyRegistered;
    public Action? OnKeyUnregistered;

    public Action? OnGameStarted;
    public Action? OnGamePaused;

    // Map each object's "Type" (the original node Name) to the scene that should be instantiated.
    // Populate these from the Editor or load them by convention, e.g. res://objects/{type}.tscn
    [Export] public Godot.Collections.Dictionary<string, PackedScene> ClearConditionScenes { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<string, PackedScene> CloneScenes { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<string, PackedScene> HazardScenes { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<string, PackedScene> OnOffScenes { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<string, PackedScene> SemiSolidTileScenes { get; set; } = new();

    public override void _Ready()
    {
        instance = this;

        level = new LevelData();

        ui = GetNode<Ui>("CanvasLayer/UI");
        ui.Visible = true;

        fileButton = GetNode<FileButton>("CanvasLayer/UI/ToolBar/FileButton");
        fileButton.filePicked += FilePicked;
        fileButton.fileSaved += FileSaved;

        EditorGameManager.GetInstance().canPause = false;

        CanvasEffects canvasEffects = CanvasEffects.GetInstance();

        canvasEffects.OnFadeOut += OnFadeOut;

        scrollButton = GetNode<ScrollButtonMenusController>("CanvasLayer/UI/ScrollButton");

        SetGameState(true);

        GetWindow().FocusExited += GetNode<ToolBar>("CanvasLayer/UI/ToolBar").CloseMenus;

        // change resolution to match editor requirements
        //Window window = GetWindow();

        //Vector2I newRes = new(1728, 864);

        //window.Size = newRes;

        //window.ContentScaleSize = newRes;

        //window.Position = (DisplayServer.ScreenGetSize(window.CurrentScreen) - window.Size) / 2;
    }

    public override void _Process(double delta)
    {
        if (gorgonzola != null)
        {
            Gorgonzola.GetInstance().OnKilled += OnGorgKilled;
        }
    }

    void FilePicked(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string fileExtension = Path.GetExtension(filePath);

        if (fileExtension != ".taglevel")
        {
            GD.PrintErr($"Invalid format: {fileExtension}");
            return;
        }

        this.filePath = filePath;

        ui.toolBarLabel.Text = fileName;

        string jsonString = File.ReadAllText(filePath);

        try
        {
            ImportLevel(GetNode("level"), LevelData.Decode(jsonString));
        }
        catch
        {
            GD.PrintErr("Invalid TAGLEVEL!");
        }
    }

    public void FileSaved(string filePath)
    {
        LevelData data = new();
        Node2D levelRoot = GetTree().CurrentScene.GetNode<Node2D>("level");

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

        if (!DirAccess.DirExistsAbsolute("user://TAGLEVELs"))
        {
            DirAccess.MakeDirAbsolute("user://TAGLEVELs");
        }

        using Godot.FileAccess file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);

        if (Path.GetFileName(filePath) != ".taglevel")
        {
            ui.toolBarLabel.Text = Path.GetFileNameWithoutExtension(filePath);
        }

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

    public void ClearGroups()
    {
        Node levelRoot = GetNode("level/level_assets");
        foreach (Node node in levelRoot.GetChildren())
        {
            if (node.Name == "clones" || node.Name == "hazards" || node.Name == "on_off_assets" || node.Name == "clear_conditions" || node.Name == "semi_solid_tiles")
            {
                foreach (Node node2 in node.GetChildren())
                {
                    node2.QueueFree();
                }
            }
        }
        Node2D tilesGroup = GetTree().CurrentScene.GetNode<Node2D>("level/tiles");

        foreach (TileMapLayer tileMapLayer in tilesGroup.GetChildren())
        {
            tileMapLayer.Clear();
        }
    }

    public async Task ImportLevel(Node levelRoot, string json)
    {
        GlobalGameManager ggm = GlobalGameManager.GetInstance();

        LevelData data = JsonSerializer.Deserialize<LevelData>(
            json,
            new JsonSerializerOptions { IncludeFields = true });

        if (data == null)
        {
            GD.PrintErr("Failed to parse TAGLEVEL.");
            return;
        }

        ClearGroups();

        // so apparently queuefree waits until the end of the frame to dispose of an object,
        // which is pretty bad for our use case.
        // fix? make the method async and wait a frame before importing anything
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        level = data;

        ImportTiles(levelRoot, data);
        ImportObjects(levelRoot, data);

        SetGameState(true);

        ui.UpdateGameButtonStates();
    }

    private void ImportTiles(Node levelRoot, LevelData data)
    {
        Node2D tilesRoot = levelRoot.GetNode<Node2D>("tiles");

        foreach (LayerData layerData in data.Layers)
        {
            TileMapLayer layer = tilesRoot.GetNodeOrNull<TileMapLayer>(layerData.Name);

            if (layer == null)
            {
                GD.PrintErr($"No TileMapLayer named '{layerData.Name}' found under 'tiles'. Skipping.");
                continue;
            }

            // Clear existing cells so re-importing doesn't leave stale tiles behind.
            layer.Clear();

            foreach (TileData tile in layerData.Tiles)
            {
                layer.SetCell(
                    new Vector2I(tile.X, tile.Y), tile.SourceId, new Vector2I(tile.AtlasX, tile.AtlasY), tile.Alternative);
            }
        }
    }

    private void ImportObjects(Node levelRoot, LevelData data)
    {
        Node2D assetsRoot = levelRoot.GetNode<Node2D>("level_assets");

        ImportObjectGroup(assetsRoot, "clear_conditions", data.ClearConditions, ClearConditionScenes);
        ImportObjectGroup(assetsRoot, "clones", data.Clones, CloneScenes);
        ImportObjectGroup(assetsRoot, "hazards", data.Hazards, HazardScenes);
        ImportOnOffGroup(assetsRoot, "on_off_assets", data.OnOffs, OnOffScenes);
        ImportSemiSolidGroup(assetsRoot, "semi_solid_tiles", data.SemiSolidTiles, SemiSolidTileScenes);
    }

    private void ImportObjectGroup(Node2D assetsRoot, string groupNodeName, List<ObjectData> objects, Godot.Collections.Dictionary<string, PackedScene> sceneMap)
    {
        Node2D groupNode = assetsRoot.GetNodeOrNull<Node2D>(groupNodeName);

        if (groupNode == null)
        {
            GD.PrintErr($"No node named '{groupNodeName}' found under 'level_assets'. Skipping.");
            return;
        }

        foreach (ObjectData obj in objects)
        {
            if (!sceneMap.TryGetValue(obj.Type, out PackedScene scene) || scene == null)
            {
                GD.PrintErr($"No scene mapped for type '{obj.Type}' in group '{groupNodeName}'. Skipping.");
                continue;
            }

            Node2D instance = scene.Instantiate<Node2D>();
            instance.Name = obj.Name;
            instance.GlobalPosition = new Vector2(obj.Position.X, obj.Position.Y);

            groupNode.AddChild(instance);

            instance.AddToGroup("editor_placeable");
        }
    }

    private void ImportSemiSolidGroup(Node2D assetsRoot, string groupNodeName, List<SemiSolidTileData> objects, Godot.Collections.Dictionary<string, PackedScene> sceneMap)
    {
        Node2D groupNode = assetsRoot.GetNodeOrNull<Node2D>(groupNodeName);

        if (groupNode == null)
        {
            GD.PrintErr($"No node named '{groupNodeName}' found under 'level_assets'. Skipping.");
            return;
        }

        foreach (SemiSolidTileData semi_solid_tile in objects)
        {
            if (!sceneMap.TryGetValue(semi_solid_tile.Type, out PackedScene scene) || scene == null)
            {
                GD.PrintErr($"No scene mapped for type '{semi_solid_tile.Type}' in group '{groupNodeName}'. Skipping.");
                continue;
            }

            Node2D instance = scene.Instantiate<Node2D>();
            instance.Name = semi_solid_tile.Name;
            instance.Position = new Vector2(semi_solid_tile.Position.X, semi_solid_tile.Position.Y);
            instance.Scale = semi_solid_tile.Scale;

            groupNode.AddChild(instance);

            GD.Print(instance.GetType());

            instance.AddToGroup("editor_placeable");
        }
    }

    private void ImportOnOffGroup(Node2D assetsRoot, string groupNodeName, OnOffAssetData asset, Godot.Collections.Dictionary<string, PackedScene> sceneMap)
    {
        Node2D groupNode = assetsRoot.GetNodeOrNull<Node2D>(groupNodeName);

        if (groupNode == null)
        {
            GD.PrintErr($"No node named '{groupNodeName}' found under 'level_assets'.");
            return;
        }

        if (asset == null)
        {
            return;
        }

        // Master
        if (asset.OnOffSwitchMaster != null && sceneMap.TryGetValue(asset.OnOffSwitchMaster.Type, out PackedScene masterScene))
        {
            Node2D master = masterScene.Instantiate<Node2D>();

            master.Name = asset.OnOffSwitchMaster.Name;
            master.GlobalPosition = asset.OnOffSwitchMaster.Position;

            groupNode.AddChild(master);

            master.AddToGroup("editor_placeable");

            (master as OnOffSwitchMaster).SetState(asset.OnOffSwitchMaster.Opened);
        }

        // Switch
        foreach (ObjectData switchData in asset.OnOffSwitches)
        {
            if (!sceneMap.TryGetValue(switchData.Type, out PackedScene switchScene))
            {
                GD.PrintErr($"No scene mapped for type '{switchData.Type}'.");
                continue;
            }

            Node2D onOffSwitch = switchScene.Instantiate<Node2D>();

            onOffSwitch.Name = switchData.Name;
            onOffSwitch.GlobalPosition = switchData.Position;

            groupNode.AddChild(onOffSwitch);

            onOffSwitch.AddToGroup("editor_placeable");
        }

        // Blocks
        if (asset.OnOffBlocks != null)
        {
            foreach (ObjectData blockData in asset.OnOffBlocks)
            {
                if (!sceneMap.TryGetValue(blockData.Type, out PackedScene blockScene))
                {
                    GD.PrintErr($"No scene mapped for type '{blockData.Type}'.");
                    continue;
                }

                Node2D block = blockScene.Instantiate<Node2D>();

                block.Name = blockData.Name;
                block.GlobalPosition = blockData.Position;

                groupNode.AddChild(block);

                block.AddToGroup("editor_placeable");

                (block as OnOffBlock).RefreshState();
            }
        }
    }


    public void ResetGame()
    {
        Input.ActionPress("reset");
    }

    public void OnGorgKilled()
    {
        CanvasEffects.GetInstance().FadeOut(new(96f / 255f, 0f, 0f, 1f));
    }

    public async void OnFadeOut()
    {
        ImportLevel(GetNode("level"), LevelData.Decode(File.ReadAllText(Path.Combine(OS.GetUserDataDir(), "tmp/.taglevel"))));

        ((Main)GetTree().CurrentScene).GetNode<Camera2D>("Camera2D").GlobalPosition = new(-224, -400);

        CanvasEffects.GetInstance().FadeIn();
    }

    public void SetGameState(bool paused)
    {
        GetTree().Paused = paused;

        Button playButton = GetNode<Button>("CanvasLayer/UI/GameControlsMenu/PlayButtonControl/CenterContainer/PlayButton");

        if (paused)
        {
            playButton.Text = "Play";
            OnGamePaused?.Invoke();
        }
        else
        {
            playButton.Text = "Stop";
            OnGameStarted?.Invoke();
        }
    }

    public void ToggleGameState()
    {
        SetGameState(!GetTree().Paused);
    }

    public void RegisterDoor(Door door)
    {
        this.door = door;
        OnDoorRegistered?.Invoke();
        GD.Print("Door registered.");
    }

    public void UnregisterDoor()
    {
        door = null;
        OnDoorUnregistered?.Invoke();
    }
    public void RegisterKey(Key key)
    {
        this.key = key;
        OnKeyRegistered?.Invoke();
    }

    public void UnregisterKey()
    {
        key = null;
        OnKeyUnregistered?.Invoke();
    }
}