using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

public partial class Runtime : Node2D
{
    // game state
    [Export] public bool editorMode = false;

    // game mode references
    Node2D gameContainer;
    [Export] PackedScene gameScene;
    Vector2I gameRes = new(1248, 832);

    // editor mode references
    Node2D editorContainer;
    [Export] PackedScene editorScene;
    Vector2I editorRes = new(1728, 864);

    // level import/export references
    public string filePath = string.Empty;
    string levelRootPath;
    LevelData level;
    [Export] public Godot.Collections.Dictionary<string, PackedScene> LevelMechanicScenes { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<string, PackedScene> CloneScenes { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<string, PackedScene> HazardScenes { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<string, PackedScene> OnOffScenes { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<string, PackedScene> SemiSolidTileScenes { get; set; } = new();

    // import/export events
    public event Action<string>? OnFileSaved;
    public event Action<string>? OnFilePicked;
    public event Action? OnLevelImported;

    public override void _Ready()
    {
        levelRootPath = editorMode ? "editor/main/level" : "game/main/level";
        gameContainer = GetNode<Node2D>("game");

        editorContainer = GetNode<Node2D>("editor");

        if (editorMode)
        {
            SwitchToEditor(false);
        }
        else
        {
            SwitchToGame(false);
        }
    }

    public override void _Process(double delta)
    {
        levelRootPath = editorMode ? "editor/main/level" : "game/main/level";

        if (editorMode && editorContainer.GetChildren().Count > 0)
        {
            GetWindow().Title = GetNode<LevelEditorMain>("editor/main").ui.toolBarLabel.Text + " - TAG Level Editor";
        }
    }

    public void SwitchToEditor(bool fade)
    {
        editorMode = true;

        if (fade)
        {
            FadePanel fadePanel = FadePanel.GetInstance();

            fadePanel.OnFadeOut += OnFadeOut;

            fadePanel.FadeOut();
        }
        else
        {
            SwitchModes();
        }

        level = new LevelData();
    }

    public void SwitchToGame(bool fade)
    {
        editorMode = false;

        if (fade)
        {
            FadePanel fadePanel = FadePanel.GetInstance();

            fadePanel.OnFadeOut += OnFadeOut;

            fadePanel.FadeOut();
        }
        else
        {
            SwitchModes();
        }
    }

    private async void OnFadeOut()
    {
        SwitchModes();

        FadePanel fadePanel = FadePanel.GetInstance();

        fadePanel.FadeIn(fadePanel.fadeinTime);

        FadePanel.GetInstance().OnFadeOut -= OnFadeOut;
    }

    private async void SwitchModes()
    {
        Node2D mode;
        Node2D oldMode = editorMode ? gameContainer : editorContainer;
        Node2D target = editorMode ? editorContainer : gameContainer;
        Window window = GetWindow();
        Vector2I targetRes = editorMode ? editorRes : gameRes;

        foreach (Node child in oldMode.GetChildren())
        {
            child.QueueFree();
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        mode = editorMode ? editorScene.Instantiate<Node2D>() : gameScene.Instantiate<Node2D>();

        DisplayServer.WindowSetSize(targetRes);

        if (editorMode)
        {
            window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        }
        else
        {
            window.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
            window.Title = "The Amazing Gorgonzola";
        }

        target.AddChild(mode);
    }

    public void FileSaved(string filePath)
    {
        LevelData data = new();
        Node2D levelRoot = GetTree().CurrentScene.GetNode<Node2D>(levelRootPath);

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
        Node2D level_mechanics = assetsRoot.GetNodeOrNull<Node2D>("level_mechanics");
        Node2D clones = assetsRoot.GetNodeOrNull<Node2D>("clones");
        Node2D hazards = assetsRoot.GetNodeOrNull<Node2D>("hazards");
        Node2D on_off_assets = assetsRoot.GetNodeOrNull<Node2D>("on_off_assets");
        Node2D semi_solid_tiles = assetsRoot.GetNodeOrNull<Node2D>("semi_solid_tiles");

        if (level_mechanics != null)
        {
            foreach (Node2D level_essential in level_mechanics.GetChildren())
            {
                data.LevelMechanics.Add(new ObjectData(level_essential.GetType().Name, level_essential.Name, new Vector2(level_essential.GlobalPosition.X, level_essential.GlobalPosition.Y)));
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

        OnFileSaved.Invoke(filePath);

        file.StoreString(SaveManager.Encode(json));
    }

    public async void FilePicked(string filePath)
    {
        this.filePath = filePath;
        string fileExtension = Path.GetExtension(filePath);

        if (fileExtension != ".taglevel")
        {
            GD.PrintErr($"Invalid format: {fileExtension}");
            return;
        }

        string jsonString = Godot.FileAccess.GetFileAsString(filePath);

        try
        {
            await ImportLevel(GetNode(levelRootPath), LevelData.Decode(jsonString));
            OnFilePicked.Invoke(filePath);
        }
        catch (Exception exception)
        {
            GD.PrintErr("Invalid TAGLEVEL!");
            GD.PrintErr(exception);
        }
    }

    public async Task ImportLevel(Node levelRoot, string json)
    {
        LevelData data = JsonSerializer.Deserialize<LevelData>(json, new JsonSerializerOptions { IncludeFields = true });

        if (data == null)
        {
            GD.PrintErr("Failed to parse TAGLEVEL.");
            return;
        }

        await ClearGroups();

        // so apparently queuefree waits until the end of the frame to dispose of an object,
        // which is pretty bad for our use case.
        // fix? make the method async and wait a frame before importing anything
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        level = data;

        ImportTiles(levelRoot, data);
        ImportObjects(levelRoot, data);

        OnLevelImported?.Invoke();
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
        Node2D level_mechanics = assetsRoot.GetNodeOrNull<Node2D>("level_mechanics");
        Node2D clones = assetsRoot.GetNodeOrNull<Node2D>("clones");
        Node2D hazards = assetsRoot.GetNodeOrNull<Node2D>("hazards");
        Node2D on_off_assets = assetsRoot.GetNodeOrNull<Node2D>("on_off_assets");
        Node2D semi_solid_tiles = assetsRoot.GetNodeOrNull<Node2D>("semi_solid_tiles");

        if (level_mechanics != null)
        {
            foreach (Node2D level_mechanic in level_mechanics.GetChildren())
            {
                data.LevelMechanics.Add(new ObjectData(level_mechanic.GetType().Name, level_mechanic.Name, new Vector2(level_mechanic.GlobalPosition.X, level_mechanic.GlobalPosition.Y)));
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

        string newFile = $"C:\\Users\\Princ\\source\\repos\\C#\\Godot Projects\\the-amazing-gorgonzola\\assets\\TAGLEVELs\\{exportName}.taglevel";

        if (!DirAccess.DirExistsAbsolute(Path.GetDirectoryName(newFile)))
        {
            DirAccess.MakeDirAbsolute(Path.GetDirectoryName(newFile));
        }

        using Godot.FileAccess file = Godot.FileAccess.Open(newFile, Godot.FileAccess.ModeFlags.Write);

        file.StoreString(SaveManager.Encode(json));
    }

    public async Task ClearGroups()
    {
        if (GetNodeOrNull<Node2D>($"{levelRootPath}/level_assets") == null)
        {
            GD.Print("[RUNTIME]: No groups to clear!");
            return;
        }

        Node levelRoot = editorMode ? GetNode($"{levelRootPath}/level_assets") : GetNode($"{levelRootPath}/level_assets");
        foreach (Node node in levelRoot.GetChildren())
        {
            if (node.Name == "clones" || node.Name == "hazards" || node.Name == "on_off_assets" || node.Name == "level_mechanics" || node.Name == "semi_solid_tiles")
            {
                foreach (Node node2 in node.GetChildren())
                {
                    node2.QueueFree();
                }
            }
        }
        Node2D tilesGroup = GetTree().CurrentScene.GetNode<Node2D>($"{levelRootPath}/tiles");

        foreach (TileMapLayer tileMapLayer in tilesGroup.GetChildren())
        {
            tileMapLayer.Clear();
        }
    }

    private void ImportObjects(Node levelRoot, LevelData data)
    {
        Node2D assetsRoot = levelRoot.GetNode<Node2D>("level_assets");

        ImportObjectGroup(assetsRoot, "level_mechanics", data.LevelMechanics, LevelMechanicScenes);
        ImportObjectGroup(assetsRoot, "clones", data.Clones, CloneScenes);
        ImportObjectGroup(assetsRoot, "hazards", data.Hazards, HazardScenes);
        ImportOnOffGroup(assetsRoot, "on_off_assets", data.OnOffs, OnOffScenes);
        ImportSemiSolidGroup(assetsRoot, "level_mechanics", data.SemiSolidTiles, LevelMechanicScenes);
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
            OnOffSwitchMaster master = masterScene.Instantiate<OnOffSwitchMaster>();

            master.Name = asset.OnOffSwitchMaster.Name;
            master.GlobalPosition = asset.OnOffSwitchMaster.Position;

            groupNode.AddChild(master);

            master.AddToGroup("editor_placeable");

            master.SetState(asset.OnOffSwitchMaster.Opened);
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

            // Clear existing cells so re-importing doesn't leave stale tiles behind
            layer.Clear();

            foreach (TileData tile in layerData.Tiles)
            {
                layer.SetCell(
                    new Vector2I(tile.X, tile.Y), tile.SourceId, new Vector2I(tile.AtlasX, tile.AtlasY), tile.Alternative);
            }
        }
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
}