using Godot;
using Ink.Parsed;
using Ink.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using static EditorObject;
using LayerData = LevelData.LayerData;
using ObjectData = LevelData.ObjectData;
using OnOffAssetData = LevelData.OnOffAssetData;
using OnOffBlockData = LevelData.OnOffBlockData;
using OnOffSwitchMasterData = LevelData.OnOffSwitchMasterData;
using Path = System.IO.Path;
using TileData = LevelData.TileData;
using FileAccess = Godot.FileAccess;

public partial class Runtime : Node2D
{
    // game state
    public static bool editorMode = false;
    [Export] public bool _editorMode {
        get => editorMode;
        set
        {
            editorMode = value;
        }
    }

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
    [Export] public Godot.Collections.Dictionary<string, PackedScene> NPCScenes { get; set; } = new();

    // import/export events
    public event Action<string>? OnFileSaved;
    public event Action<string>? OnFilePicked;
    public event Action? OnLevelImported;

    // cursor info
    [Export] Godot.Collections.Dictionary<string, Texture2D> cursorCache = new();
    private static readonly Input.CursorShape[] AllShapes = {
    Input.CursorShape.Arrow,
    Input.CursorShape.Ibeam,
    Input.CursorShape.PointingHand,
    Input.CursorShape.Cross,
    Input.CursorShape.Wait,
    Input.CursorShape.Busy,
    Input.CursorShape.Drag,
    Input.CursorShape.CanDrop,
    Input.CursorShape.Forbidden,
    Input.CursorShape.Vsize,
    Input.CursorShape.Hsize,
    Input.CursorShape.Bdiagsize,
    Input.CursorShape.Fdiagsize,
    Input.CursorShape.Move,
    Input.CursorShape.Vsplit,
    Input.CursorShape.Hsplit,
    Input.CursorShape.Help};
    BaseButton hoveredButton;

    // game directories and log file
    private static string progFlagsPath = "PROGFLAGS";
    private static string saveFile = "save.dat";
    private static string debugFile = "supersecretdebugvariablenobodyknowsaboutexceptmeakathedeveloperwhoissuperawesomeandcool.youllneverguesswhatvalueissupposedtobeinheremwahahahahaha";
    private static string logFilePath = "LOG";
    private static string logFile = "console_log.txt";
    private static string logDumpPath = "LOGDUMPS";
    private static string tagLevelsPath = "TAGLEVELS";


    public override void _Ready()
    {
        DebugManager.BuildCommands();

        GetTree().NodeAdded += CheckForHookableButtons;

        // Handle buttons that already exist in the scene.
        SetButtonCursorRecursive(GetTree().Root);

        HookExistingButtons(GetTree().Root);

        if (!cursorCache.TryGetValue("Point", out Texture2D texture) || texture == null)
        {
            DebugManager.Log("No texture mapped for type 'Point' in cursor cache.", "Runtime");
        }

        levelRootPath = _editorMode ? "editor/main/level" : "game/main/level";
        gameContainer = GetNode<Node2D>("game");
        editorContainer = GetNode<Node2D>("editor");

        if (_editorMode)
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
        levelRootPath = _editorMode ? "editor/main/level" : "game/main/level";

        if (hoveredButton != null &&
        (!IsInstanceValid(hoveredButton) || hoveredButton.Disabled || !hoveredButton.IsVisibleInTree()))
        {
            hoveredButton = null;

            if (GetNodeOrNull<LevelEditorMain>("editor/main") != null)
            {
                ApplyCursor(GetNode<EditorObject>("editor/main/EditorObject").currentCursorMode.ToString());
            }
            else
            {
                ApplyCursor("Mouse");
            }
        }

        if (Input.IsActionJustPressed("F5"))
        {
            if (_editorMode)
            {
                ExportLevelAsScene("C:/Users/Princ/source/repos/C#/Godot Projects/the-amazing-gorgonzola/assets/Levels/Scenes/Exported");
            }
            else
            {
                GlobalGameManager ggm = GlobalGameManager.GetInstance();
                ggm.LoadNextLevel();
                ggm.OnNextLevelLoaded += OnLevelSkipped;
            }
        }

        if (DebugManager.debug)
        {
            if (Input.IsActionJustPressed("open_console"))
            {
                DebugManager.ToggleDebugConsole();
            }
        }
        else
        {
            DebugManager.debugConsoleActive = false;
        }

        if (DebugManager.debugConsoleActive)
        {
            DebugConsole.GetInstance().SetActive(true);
            if (Input.IsActionJustPressed("return"))
            {
                if (DebugConsole.GetInstance().GetInputText() != "")
                {
                    DebugManager.ProcessCommand(DebugConsole.GetInstance().GetInputText());
                }

                DebugConsole.GetInstance().ClearInputText();
            }
        }
        else
        {
            DebugConsole.GetInstance().SetActive(false);
        }

        if (DebugConsole.GetInstance().GetInputText() == "")
        {
            DebugManager.typing = false;
        }
        else
        {
            DebugManager.typing = true;
        }
    }

    void OnLevelSkipped()
    {
        GlobalGameManager ggm = GlobalGameManager.GetInstance();
        ggm.OnNextLevelLoaded -= OnLevelSkipped;
        ggm.canMove = true;
    }

    private void CheckForHookableButtons(Node node)
    {
        // NodeAdded fires for every node individually, so no recursion needed here
        if (node is BaseButton button)
        {
            HookButton(button);
        }
    }

    private void HookExistingButtons(Node node)
    {
        if (node is BaseButton button)
        {
            HookButton(button);
        }

        foreach (Node child in node.GetChildren())
        {
            HookExistingButtons(child);
        }
    }

    private void HookButton(BaseButton button)
    {
        // Guard against double-hooking (e.g. node reparented -> NodeAdded fires again)
        if (button.HasMeta("cursor_hooked"))
        {
            return;
        }

        button.SetMeta("cursor_hooked", true);

        button.MouseEntered += () => OnButtonMouseEntered(button);
        button.MouseExited += () => OnButtonMouseExited(button);
    }

    private void OnButtonMouseEntered(BaseButton button)
    {
        if (button.Disabled)
        {
            return; // disabled buttons keep the current cursor
        }

        hoveredButton = button;
        ApplyCursor("Point");
    }

    private void OnButtonMouseExited(BaseButton button)
    {
        // Only react if this is the button we think we're on
        if (hoveredButton != button)
        {
            return;
        }

        hoveredButton = null;

        if (GetNodeOrNull<LevelEditorMain>("editor/main") != null)
        {
            ApplyCursor(GetNode<EditorObject>("editor/main/EditorObject").currentCursorMode.ToString());
        }
        else
        {
            ApplyCursor("Mouse");
        }
    }

    private void SetButtonCursorRecursive(Node node)
    {
        if (node is BaseButton button)
        {
            button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        }

        foreach (Node child in node.GetChildren())
        {
            SetButtonCursorRecursive(child);
        }
    }

    public void SwitchToEditor(bool fade)
    {
        _editorMode = true;

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
        _editorMode = false;

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
        GetTree().AutoAcceptQuit = true;

        Node2D mode;
        Node2D oldMode = _editorMode ? gameContainer : editorContainer;
        Node2D target = _editorMode ? editorContainer : gameContainer;
        Window window = GetWindow();
        Vector2I targetRes = _editorMode ? editorRes : gameRes;

        foreach (Node child in oldMode.GetChildren())
        {
            child.QueueFree();
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        mode = _editorMode ? editorScene.Instantiate<Node2D>() : gameScene.Instantiate<Node2D>();

        DisplayServer.WindowSetSize(targetRes);

        if (_editorMode)
        {
            window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            ApplyCursor(CursorMode.Place.ToString());
        }
        else
        {
            window.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
            window.Title = "The Amazing Gorgonzola";
            ApplyCursor(CursorMode.Mouse.ToString());
        }

        target.AddChild(mode);
    }

    /// <summary>
    /// Returns the path of the save file, creates one if nonexistent
    /// </summary>
    /// <returns></returns>
    public static string GetSavePath()
    {
        string saveFilePath = Path.Combine(OS.GetUserDataDir(), saveFile);

        if (!File.Exists(saveFilePath))
        {
            using Godot.FileAccess file = Godot.FileAccess.Open(saveFilePath, Godot.FileAccess.ModeFlags.Write);

            if (file == null)
            {
                Error error = Godot.FileAccess.GetOpenError();

                DebugManager.Log($"Failed to create log file '{saveFilePath}': {error}", "Runtime", true);
                DebugManager.Log($"Could not create log file: {saveFilePath}", "Runtime", true);
            }
        }

        return saveFilePath;
    }

    /// <summary>
    /// Creates a file in the log dump directory with a timestamped name and returns the path to the file for writing.
    /// </summary>
    /// <returns></returns>
    public static string CreateLogDump()
    {
        string logDirectory = InitializeLogDumpsPath();

        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch (Exception e)
        {
            DebugManager.Log($"Failed to create log directory '{logDirectory}': {e.Message}", "Runtime", true);
            throw;
        }

        string logFileName = $"log_dump_{DebugManager.GetCurrentDateTimeFormatted()}.txt";
        string logPath = Path.Combine(logDirectory, logFileName);

        using Godot.FileAccess file = Godot.FileAccess.Open(logPath, Godot.FileAccess.ModeFlags.Write);

        if (file == null)
        {
            Error error = Godot.FileAccess.GetOpenError();

            DebugManager.Log($"Failed to create log file '{logPath}': {error}", "Runtime", true);
            DebugManager.Log($"Could not create log file: {logPath}", "Runtime", true);
        }

        return logPath;
    }

    /// <summary>
    /// Gets the path to the console's log file, creates one if it doesn't exist.
    /// </summary>
    /// <returns></returns>
    public static string GetLogPath()
    {
        string logPath = Path.Combine(InitializeLogPath(), logFile);

        if (!File.Exists(logPath))
        {
            using Godot.FileAccess file = Godot.FileAccess.Open(logPath, Godot.FileAccess.ModeFlags.Write);

            if (file == null)
            {
                Error error = Godot.FileAccess.GetOpenError();

                DebugManager.Log($"Failed to create log file '{logPath}': {error}", "Runtime", true);
                DebugManager.Log($"Could not create log file: {logPath}", "Runtime", true);
            }
        }

        return logPath;
    }

    /// <summary>
    /// Checks for the existence of a debug permission file and then checks that file's contents
    /// </summary>
    /// <returns>False if the file does not exist or if the contents are unexpected, true if file exist and gives a valid response.</returns>
    public static bool CheckIfDebugKeyExists()
    {
        string debugFilePath = Path.Combine(InitializeProgFlagsPath(), debugFile);
        bool debugKeyExists = true;
        if (!File.Exists(debugFilePath))
        {
            debugKeyExists = false;
        }
        if (File.ReadAllText(debugFilePath).Trim() != "1")
        {
            debugKeyExists = false;
        }
        return debugKeyExists;
    }

    /// <summary>   
    /// Initializes the PROGFLAGS directory in the user data folder and returns the path to it.
    /// </summary>
    /// <returns>The path to the program flags directory</returns>
    public static string InitializeProgFlagsPath()
    {
        string progFlagsDirectory = Path.Combine(OS.GetUserDataDir(), progFlagsPath);
        if (!DirAccess.DirExistsAbsolute(progFlagsDirectory))
        {
            Error error = DirAccess.MakeDirRecursiveAbsolute(progFlagsDirectory);

            if (error != Error.Ok)
            {
                DebugManager.Log($"Failed to create directory. Error: {error}", "Runtime", true);
            }
        }
        return progFlagsDirectory;
    }

    /// <summary>
    /// Initializes Console Log Directory in User Data.
    /// </summary>
    /// <returns>The path to the log directory.</returns>
    public static string InitializeLogPath()
    {
        string logDirectory = Path.Combine(OS.GetUserDataDir(), logFilePath);

        if (!Directory.Exists(logDirectory))
        {
            try
            {
                Directory.CreateDirectory(logDirectory);
            }
            catch (Exception e)
            {
                DebugManager.Log($"Failed to create user data log directory '{logDirectory}': {e.Message}", "Runtime", true);
                return string.Empty;
            }
        }

        return logDirectory;
    }

    /// <summary>
    /// Initializes Console Dump Directory in Documents and returns the path.
    /// </summary>
    /// <returns>The path of the log directory.</returns>
    public static string InitializeLogDumpsPath()
    {
        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string logDirectory = Path.Combine(documentsPath, "The Amazing Gorgonzola", logDumpPath);
        if (!Directory.Exists(logDirectory))
        {
            try
            {
                Directory.CreateDirectory(logDirectory);
            }
            catch (Exception e)
            {
                DebugManager.Log($"Failed to create documents log directory '{logDirectory}': {e.Message}", "Runtime", true);
                return string.Empty;
            }
        }

        return logDirectory;
    }

    public static string GetLevelsDirectory()
    {
        // OS.GetUserDataDir() is already an absolute OS path, in editor and release
        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string levelsDirectory = Path.Combine(documentsPath, "The Amazing Gorgonzola", tagLevelsPath);

        if (!Directory.Exists(levelsDirectory))
        {
            try
            {
                Directory.CreateDirectory(levelsDirectory);
            }
            catch (Exception e)
            {
                DebugManager.Log($"Failed to create levels directory '{levelsDirectory}': {e.Message}", "Runtime", true);
                return string.Empty;
            }
        }

        return levelsDirectory;
    }

    public void FileSaved(string filePath)
    {
        LevelData data = new();
        Node2D levelRoot = GetTree().CurrentScene.GetNode<Node2D>(levelRootPath);

        // if the game is running, there may be some objects that we don't want to export
        // fix? save the tmp level to the path if game is running
        if (!GetTree().Paused)
        {
            string tmpPath = Path.Combine(OS.GetUserDataDir(), "tmp/.taglevel");

            //replace forward slashes with back slashes for valid path recognition
            tmpPath = tmpPath.Replace("/", "\\");

            File.Copy(tmpPath, filePath, true);
        }
        else
        {
            foreach (Node2D tileMapLayer in levelRoot.GetNode<Node2D>("tiles").GetChildren())
            {
                if (tileMapLayer is not TileMapLayer layer)
                {
                    continue;
                }

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
            Node2D npcs = assetsRoot.GetNodeOrNull<Node2D>("npcs");
            Node2D semi_solid_tiles = assetsRoot.GetNodeOrNull<Node2D>("semi_solid_tiles");

            if (level_mechanics != null)
            {
                foreach (Node2D level_essential in level_mechanics.GetChildren())
                {
                    data.LevelMechanics.Add(new ObjectData(level_essential.GetType().Name, level_essential.GlobalPosition));
                }
            }

            if (clones != null)
            {
                foreach (Node2D node in clones.GetChildren())
                {
                    if (node is BasePlayerController clone)
                    {
                        data.Clones.Add(new ObjectData(clone.cloneType.ToString(), clone.GlobalPosition));
                    }
                    else if (node is ShadowCloneSpawn shadowCloneSpawn)
                    {
                        data.Clones.Add(new ObjectData(shadowCloneSpawn.GetType().Name, shadowCloneSpawn.GlobalPosition));
                    }
                }
            }

            if (on_off_assets != null)
            {
                foreach (Node2D on_off_asset in on_off_assets.GetChildren())
                {
                    if (on_off_asset is OnOffSwitchMaster switchMaster)
                    {
                        data.OnOffs.OnOffSwitchMaster = new OnOffSwitchMasterData(switchMaster.GetType().ToString(), on_off_asset.GlobalPosition, switchMaster.opened);
                    }
                    else if (on_off_asset is OnOffSwitch switchNormal)
                    {
                        data.OnOffs.OnOffSwitches.Add(new ObjectData(switchNormal.GetType().ToString(), on_off_asset.GlobalPosition));
                    }
                    else if (on_off_asset is OnOffBlock onOffBlock)
                    {
                        data.OnOffs.OnOffBlocks.Add(new OnOffBlockData(onOffBlock.green ? OnOffBlockData.blockTypes.green : OnOffBlockData.blockTypes.red, on_off_asset.GlobalPosition));
                    }
                }
            }

            if (npcs != null)
            {
                foreach (Npc npc in npcs.GetChildren())
                {
                    data.NPCs.Add(new ObjectData(npc.npcType.ToString(), npc.GlobalPosition));
                }
            }

            if (hazards != null)
            {
                foreach (Node2D hazard in hazards.GetChildren())
                {
                    data.Hazards.Add(new ObjectData(hazard.GetType().Name, hazard.GlobalPosition));
                }
            }

            if (semi_solid_tiles != null)
            {
                foreach (Node2D semi_solid_tile in semi_solid_tiles.GetChildren())
                {
                    data.SemiSolidTiles.Add(new ObjectData(semi_solid_tile.GetType().Name, semi_solid_tile.GlobalPosition));
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

            using FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);

            file.StoreString(SaveManager.Encode(json));
        }

        OnFileSaved.Invoke(filePath);
    }

    public async void FilePicked(string filePath)
    {
        this.filePath = filePath;

        if (!File.Exists(filePath))
        {
            DebugManager.Log($"File not found: {filePath}", "RuntimeLevelService");
            return;
        }

        string fileExtension = Path.GetExtension(filePath);

        if (fileExtension != ".taglevel")
        {
            DebugManager.Log($"Invalid format: {fileExtension}", "RuntimeLevelService");
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
            DebugManager.Log("Invalid TAGLEVEL!", "Runtime", true);
            DebugManager.Log(exception, "Runtime", true);
        }
    }

    public async Task ImportLevel(Node levelRoot, string json)
    {
        LevelData data = JsonSerializer.Deserialize<LevelData>(json, new JsonSerializerOptions { IncludeFields = true });

        if (data == null)
        {
            DebugManager.Log("Failed to parse TAGLEVEL.", "RuntimeLevelService");
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

    public async Task ClearGroups()
    {
        if (GetNodeOrNull<Node2D>($"{levelRootPath}/level_assets") == null)
        {
            DebugManager.Log("No groups to clear!", "RuntimeLevelService");
            return;
        }

        Node levelRoot = _editorMode ? GetNode($"{levelRootPath}/level_assets") : GetNode($"{levelRootPath}/level_assets");
        foreach (Node node in levelRoot.GetChildren())
        {
            if (node.Name == "clones" || node.Name == "hazards" || node.Name == "on_off_assets" || node.Name == "level_mechanics" || node.Name == "npcs" || node.Name == "semi_solid_tiles")
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
        ImportObjectGroup(assetsRoot, "npcs", data.NPCs, NPCScenes);
        ImportObjectGroup(assetsRoot, "level_mechanics", data.SemiSolidTiles, LevelMechanicScenes);
    }

    private void ImportObjectGroup(Node2D assetsRoot, string groupNodeName, List<ObjectData> objects, Godot.Collections.Dictionary<string, PackedScene> sceneMap)
    {
        Node2D groupNode = assetsRoot.GetNodeOrNull<Node2D>(groupNodeName);

        if (groupNode == null)
        {
            DebugManager.Log($"No node named '{groupNodeName}' found under 'level_assets'. Skipping.", "RuntimeLevelService");
            return;
        }

        foreach (ObjectData obj in objects)
        {
            if (!sceneMap.TryGetValue(obj.Type, out PackedScene scene) || scene == null)
            {
                DebugManager.Log($"No scene mapped for type '{obj.Type}' in group '{groupNodeName}'. Skipping.", "RuntimeLevelService");
                continue;
            }

            Node2D instance = scene.Instantiate<Node2D>();
            instance.GlobalPosition = new Vector2(obj.Position.X, obj.Position.Y);
            instance.Name = obj.Type;

            groupNode.AddChild(instance);

            instance.AddToGroup("editor_placeable");
        }
    }

    private void ImportOnOffGroup(Node2D assetsRoot, string groupNodeName, OnOffAssetData asset, Godot.Collections.Dictionary<string, PackedScene> sceneMap)
    {
        Node2D groupNode = assetsRoot.GetNodeOrNull<Node2D>(groupNodeName);

        if (groupNode == null)
        {
            DebugManager.Log($"No node named '{groupNodeName}' found under 'level_assets'.", "RuntimeLevelService");
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

            master.GlobalPosition = asset.OnOffSwitchMaster.Position;

            master.Name = asset.OnOffSwitchMaster.Type;

            groupNode.AddChild(master);

            master.AddToGroup("editor_placeable");

            master.SetState(asset.OnOffSwitchMaster.Opened);
        }

        // Switch
        foreach (ObjectData switchData in asset.OnOffSwitches)
        {
            if (!sceneMap.TryGetValue(switchData.Type, out PackedScene switchScene))
            {
                DebugManager.Log($"No scene mapped for type '{switchData.Type}'.", "RuntimeLevelService");
                continue;
            }

            Node2D onOffSwitch = switchScene.Instantiate<Node2D>();

            onOffSwitch.GlobalPosition = switchData.Position;

            onOffSwitch.Name = switchData.Type;

            groupNode.AddChild(onOffSwitch);

            onOffSwitch.AddToGroup("editor_placeable");
        }

        // Blocks
        if (asset.OnOffBlocks != null)
        {
            foreach (OnOffBlockData blockData in asset.OnOffBlocks)
            {
                if (!sceneMap.TryGetValue(blockData.blockType.ToString(), out PackedScene blockScene))
                {
                    DebugManager.Log($"No scene mapped for type '{blockData.blockType.ToString()}'.", "RuntimeLevelService");
                    continue;
                }

                Node2D block = blockScene.Instantiate<Node2D>();

                block.GlobalPosition = blockData.Position;

                block.Name = blockData.blockType.ToString();

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
                DebugManager.Log($"No TileMapLayer named '{layerData.Name}' found under 'tiles'. Skipping.", "Runtime");
                continue;
            }

            // Clear existing cells so re-importing doesn't leave stale tiles behind
            layer.Clear();

            foreach (TileData tile in layerData.Tiles)
            {
                layer.SetCell(new Vector2I(tile.X, tile.Y), tile.SourceId, new Vector2I(tile.AtlasX, tile.AtlasY), tile.Alternative);
            }
        }
    }

    private void ExportLevelAsScene(string folderPath)
    {
        Node duplicatedRoot = GetNode<Node>(levelRootPath).Duplicate((int)(Node.DuplicateFlags.Signals | Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts));

        // Make every descendant owned by the new root so Pack() includes it
        SetOwnerRecursive(duplicatedRoot, duplicatedRoot);

        PackedScene packedScene = new PackedScene();
        Error packError = packedScene.Pack(duplicatedRoot);

        duplicatedRoot.Free(); // not added to the tree, so Free() is correct (see below)

        if (packError != Error.Ok)
        {
            DebugManager.Log($"Failed to pack cloned scene: {packError}", "Runtime");
            return;
        }

        string path = $"{folderPath}/{DebugManager.GetCurrentDateTimeFormatted()}.tscn";
        Error saveError = ResourceSaver.Save(packedScene, path);
        if (saveError != Error.Ok)
        {
            DebugManager.Log($"Failed to save: {saveError}, ", "Runtime", true);
        }
    }

    private void SetOwnerRecursive(Node node, Node owner)
    {
        if (node != owner)
        {
            node.Owner = owner;
        }

        foreach (Node child in node.GetChildren())
        {
            SetOwnerRecursive(child, owner);
        }
    }

    public void ApplyCursor(string mode)
    {
        if (mode == "Null")
        {
            foreach (var shape in AllShapes)
            {
                Input.SetCustomMouseCursor(null, shape);
            }
            return;
        }

        if (!cursorCache.TryGetValue(mode, out Texture2D texture) || texture == null)
        {
            DebugManager.Log($"No texture mapped for type '{mode}' in cursor cache.", "Runtime");
            foreach (var shape in AllShapes)
            {
                Input.SetCustomMouseCursor(null, shape);
            }
            return;
        }

        Vector2 hotspot = mode == "Select" ? new Vector2(16, 16) : Vector2.Zero;

        foreach (var shape in AllShapes)
        {
            Input.SetCustomMouseCursor(texture, shape, hotspot);
        }
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