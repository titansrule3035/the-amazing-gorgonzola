using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The node. Mouse-to-cell math, mode switching, hotkeys, shared actions (place, erase, delete), overlay drawing, filter dropdown.
/// Behaviour lives elsewhere:
///   EditorTools.cs        - one class per cursor mode
///   EditorSelection.cs    - selection, picking, moving
///   EditorHistory.cs      - undo/redo and every undoable command
///   EditorContextMenu.cs  - right-click menu and its extension points
///   
/// To add a mode: add a CursorMode value (append it), write an EditorTool, Register() it in BuildTools(),
/// and optionally add a row to Hotkeys.
/// </summary>
public partial class EditorObject : Node2D
{
    // ------------------------------------------------------------------
    // Types
    // ------------------------------------------------------------------
    public enum CursorMode
    {
        Mouse,
        Move,
        Select,
        Place,
        Eraser,
        Copy,
        DragCamera,
        Null
    }

    public enum FilterMode
    {
        All,
        Clones,
        Enemies,
        Hazards,
        Tiles,
        LevelMechanics,
        OnOff,
        NPCs
    }

    // ------------------------------------------------------------------
    // Constants
    // ------------------------------------------------------------------
    public const string placeableGroup = "editor_placeable";
    public const string metaCell = "editor_cell";       // Vector2I: the cell an object was placed in
    public const string metaType = "editor_item_type";  // int: EditorItemObject.ItemType
    const string levelRoot = "level/level_assets";

    // Input action -> mode. Add a row here to bind a new hotkey.
    static readonly (string Action, CursorMode Mode, bool NeedsEditorActive)[] Hotkeys =
    {
        ("mouse_mouse",  CursorMode.Mouse,  false),
        ("mouse_move",   CursorMode.Move,   false),
        ("mouse_select", CursorMode.Select, false),
        ("mouse_place",  CursorMode.Place,  false),
        ("mouse_eraser", CursorMode.Eraser, false),
        ("mouse_copy",   CursorMode.Copy,   true),
    };

    // ------------------------------------------------------------------
    // Exported / inspector state
    // ------------------------------------------------------------------
    [ExportGroup("Debug")]
    [Export] public Vector2 mouseGlobal;
    [Export] public Vector2 local;
    [Export] public Vector2I cell;
    [Export] public Vector2 snappedWorld; // world position of the CENTER of the hovered tile

    [ExportGroup("Editor")]
    [Export] public bool canPlace = false; // set by your UI: true while the mouse is over the level
    [Export] public Node2D level;
    [Export] public PackedScene currentItem = null;
    [Export] public Texture2D currentTile = null;
    [Export] public CursorMode lastCursorMode = CursorMode.Null;
    [Export] public int maxUndoSteps = 200;
    [Export] public bool buildFilterUi = true; // adds a small "Select:" dropdown (top right) while the editor is active

    CursorMode mode = CursorMode.Place;

    /// <summary>Setting this (from UI, other scripts, etc.) properly switches tools.</summary>
    [Export]
    public CursorMode currentCursorMode
    {
        get => mode;
        set => SetCursorMode(value);
    }

    FilterMode filter = FilterMode.All;

    /// <summary>Which kinds of things selecting and grabbing can pick up.</summary>
    [Export]
    public FilterMode currentFilterMode
    {
        get => filter;
        set
        {
            filter = value;
            SyncFilterUi();
            FilterChanged?.Invoke(value);
        }
    }

    // ------------------------------------------------------------------
    // Shared state
    // ------------------------------------------------------------------
    public Sprite2D cursorSprite { get; private set; }
    public TileMapLayer tileMap { get; private set; }
    public EditorSelection selection { get; private set; }
    public EditorHistory history { get; private set; }
    public string levelName { get; set; } = "Untitled";

    public EditorItemObject itemObject;
    public EditorItemObject.ItemType itemType;
    public bool hideCursor = false;

    public Action? ItemPlaced;
    public Action? SelectionChanged;
    public Action? HistoryChanged;
    public event Action<FilterMode>? FilterChanged;
    public event Action? OnSaveComplete;

    // ------------------------------------------------------------------
    // Convenience accessors
    // ------------------------------------------------------------------

    /// <summary>The single "is the editor live" check. Change it here and everything follows.</summary>
    public bool editorActive => GetTree().Paused;

    public bool primaryHeld => Input.IsActionPressed("mb_left");

    /// <summary>Editor is live, the mouse is over the level, and no popup or editor UI is eating the click.</summary>
    public bool canInteract => editorActive && canPlace && !overOwnUi && Engine.GetProcessFrames() > uiBlockUntilFrame && !(contextMenu?.Menu.Visible ?? false);

    /// <summary>Offset from a tile's center to its bottom edge.</summary>
    public Vector2 halfTileDown => new(0, tileMap.TileSet.TileSize.Y / 2f);

    public bool hasPlaceableItem => itemObject != null && itemType != EditorItemObject.ItemType.Null && (currentItem != null || currentTile != null);

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------
    Dictionary<CursorMode, EditorTool> tools;
    EditorContextMenu contextMenu;
    Node2D overlay;
    CanvasLayer filterLayer;
    OptionButton filterOption;

    bool ctrlOverride;
    bool wasActive;
    bool overOwnUi;
    ulong uiBlockUntilFrame;
    private MouseButton pressedButton = MouseButton.None;
    private Vector2 pressPos;
    private bool selectionPressStarted;
    private const float ClickDragThreshold = 5f;

    static EditorObject instance;

    EditorTool activeTool => tools != null && tools.TryGetValue(mode, out var tool) ? tool : null;

    // ------------------------------------------------------------------
    // Tools / modes
    // ------------------------------------------------------------------
    void BuildTools()
    {
        tools = new();
        Register(new MouseTool(this));
        Register(new MoveTool(this));
        Register(new SelectTool(this));
        Register(new PlaceTool(this));
        Register(new EraserTool(this));
        Register(new CopyTool(this));
        Register(new DragCameraTool(this));
    }

    void Register(EditorTool tool) => tools[tool.mode] = tool;

    /// <summary>Public mode switch. Cancels any pending Ctrl override.</summary>
    public void SetCursorMode(CursorMode mode)
    {
        ctrlOverride = false;
        SwitchMode(mode);
    }

    void SwitchMode(CursorMode mode)
    {
        activeTool?.Exit();
        this.mode = mode;
        activeTool?.Enter();
        UpdateMouseCursor();
        RedrawOverlay();
    }

    void UpdateMouseCursor()
    {
        string currentCursor = mode.ToString();
    }

    // ------------------------------------------------------------------
    // Godot lifecycle
    // ------------------------------------------------------------------
    public override void _Ready()
    {
        if (instance != null)
        {
            GD.PrintErr("Error: only one EditorObject allowed, deleting this one...");
        }

        instance = this;

        cursorSprite = GetNode<Sprite2D>("Sprite");
        level = GetParent().GetNode<Node2D>("level");
        tileMap = level.GetNode<TileMapLayer>("tiles/Foreground");

        history = new EditorHistory(maxUndoSteps);
        history.Changed += OnHistoryChanged;

        selection = new EditorSelection(this);
        selection.Changed += OnSelectionChanged;

        CreateOverlay();

        contextMenu = new EditorContextMenu(this);
        AddChild(contextMenu.Menu);

        if (buildFilterUi)
        {
            CreateFilterUi();
        }

        RegisterItemContextOptions();
        BuildTools();
        activeTool?.Enter();
    }

    public static EditorObject GetInstance()
    {
        return instance;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            RequestAction(() => GetTree().Quit());
        }
    }

    /// <summary>Runs `proceed` immediately if history is clean or if already prompted, otherwise asks first.</summary>
    void RequestAction(Action proceed)
    {
        LevelEditorMain editorMain = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main");

        if (!history.IsDirty || editorMain.ui.quitMenu.Visible)
        {
            proceed();
            return;
        }

        OnSaveComplete = proceed;

        editorMain.ui.quitMenu.SetMenuVisibility(true);
        editorMain.ui.quitMenu.programMode = true;
        editorMain.ui.blockMouse.MouseFilter = Control.MouseFilterEnum.Stop;
    }

    public override void _ExitTree()
    {
        if (instance == this)
        {
            instance = null;
        }
        // Frees any objects that are only kept alive by the undo stacks.
        history?.Clear();
    }

    public override void _Process(double delta)
    {
        bool active = editorActive;

        if (active != wasActive)
        {
            wasActive = active;
            OnActiveChanged(active);
        }

        UpdateMouseCell();
        HandleModeHotkeys();

        var tool = activeTool;

        if (tool == null)
        {
            cursorSprite.Visible = false;
            return;
        }

        tool.UpdateCursor();

        if (active)
        {
            tool.Update(delta);
        }

        hideCursor = !(active && tool.cursorVisible);
        cursorSprite.Visible = !hideCursor;

        GetWindow().Title = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main").levelName + (EditorObject.GetInstance().history.IsDirty ? "* (Unsaved)" : "") + " - TAG Level Editor";

        if (history.IsDirty)
        {
            GetTree().AutoAcceptQuit = false;
        }
        else
        {
            GetTree().AutoAcceptQuit = true;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!editorActive || !canPlace)
        {
            return;
        }

        if (@event is not InputEventMouseButton mouseButton)
        {
            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        if (mouseButton.Pressed)
        {
            pressedButton = MouseButton.Left;
            pressPos = GetGlobalMousePosition();
            selectionPressStarted = !selection.isEmpty;
            return;
        }

        // Only handle the release matching our recorded press.
        if (pressedButton != MouseButton.Left)
        {
            return;
        }

        Vector2 releasePos = GetGlobalMousePosition();
        float distance = pressPos.DistanceTo(releasePos);

        // Only cancel selection on a click, not a drag.
        if (selectionPressStarted && distance < ClickDragThreshold)
        {
            Vector2I releaseCell = WorldToCell(releasePos);

            bool clickedEmptySpace = ObjectsAtCell(releaseCell).Count == 0 && QueryObjectsAt(releasePos).Count == 0 && tileMap.GetCellSourceId(releaseCell) == -1;

            if (clickedEmptySpace)
            {
                selection.Clear();
            }
        }

        pressedButton = MouseButton.None;
        selectionPressStarted = false;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!editorActive || @event is not InputEventKey { Pressed: true } key) return;

        bool command = key.IsCommandOrControlPressed();

        switch (key.Keycode)
        {
            case Godot.Key.Z when command:
                Undo();
                GetViewport().SetInputAsHandled();
                break;

            case Godot.Key.Y when command:
                Redo();
                GetViewport().SetInputAsHandled();
                break;

            case Godot.Key.Delete when !command:
            case Godot.Key.Backspace when !command:
                DeleteSelection();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            return;
        }

        if (!canInteract)
        {
            return;
        }

        if (mode is CursorMode.Mouse or CursorMode.DragCamera)
        {
            return;
        }

        if (activeTool?.isBusy == true)
        {
            return;
        }

        contextMenu.Open();
        GetViewport().SetInputAsHandled();
    }

    void OnActiveChanged(bool active)
    {
        overlay.Visible = active;

        if (filterLayer != null)
        {
            filterLayer.Visible = active;
        }

        if (!active)
        {
            overOwnUi = false;
            activeTool?.OnEditorDeactivated();
        }

        RedrawOverlay();
    }

    // ------------------------------------------------------------------
    // Per-frame helpers
    // ------------------------------------------------------------------
    void UpdateMouseCell()
    {
        mouseGlobal = GetGlobalMousePosition();
        local = tileMap.ToLocal(mouseGlobal);
        cell = tileMap.LocalToMap(local);
        snappedWorld = tileMap.ToGlobal(tileMap.MapToLocal(cell));
    }

    void HandleModeHotkeys()
    {
        foreach (var (action, mode, needsEditor) in Hotkeys)
        {
            if (needsEditor && !editorActive) continue;

            if (Input.IsActionJustPressed(action))
            {
                SetCursorMode(mode);
                ((Runtime)GetTree().CurrentScene).ApplyCursor(mode.ToString());
            }
        }

        // Ctrl temporarily becomes the plain mouse, then restores the previous mode.
        // Tools can opt out (Select uses Ctrl for "add to selection", and drags must not be interrupted).
        if (Input.IsActionJustPressed("ctrl") && mode != CursorMode.Mouse && activeTool is { allowCtrlMouseOverride: true })
        {
            lastCursorMode = mode;
            ctrlOverride = true;
            SwitchMode(CursorMode.Mouse);
        }

        if (Input.IsActionJustReleased("ctrl") && ctrlOverride)
        {
            ctrlOverride = false;

            if (lastCursorMode != CursorMode.Null)
            {
                SwitchMode(lastCursorMode);
            }
        }
    }

    // ------------------------------------------------------------------
    // Grid / object helpers
    // ------------------------------------------------------------------
    public Vector2 CellToWorld(Vector2I c) => tileMap.ToGlobal(tileMap.MapToLocal(c));

    public Vector2I WorldToCell(Vector2 world) => tileMap.LocalToMap(tileMap.ToLocal(world));

    /// <summary>World-space rectangle covering one grid cell.</summary>
    public Rect2 CellRect(Vector2I c)
    {
        Vector2 size = tileMap.TileSet.TileSize;
        return new Rect2(CellToWorld(c) - size / 2f, size);
    }

    /// <summary>
    /// The cell a placed object belongs to. Uses the cell stored when it was placed or moved.
    /// (An object's origin sits on a cell's bottom edge, so deriving the cell from position alone is unreliable.)
    /// Objects without that data fall back to "one half-tile above the origin".
    /// </summary>
    public Vector2I GetObjectCell(Node2D obj)
    {
        if (obj.HasMeta(metaCell))
        {
            return obj.GetMeta(metaCell).AsVector2I();
        }

        return WorldToCell(obj.GlobalPosition - halfTileDown);
    }

    public EditorItemObject.ItemType GetObjectType(Node2D obj)
    {
        if (obj.HasMeta(metaType))
        {
            return (EditorItemObject.ItemType)obj.GetMeta(metaType).AsInt32();
        }

        // Fallback for objects that weren't placed this session: infer from their container.
        return obj.GetParent()?.Name.ToString() switch
        {
            "clones" => EditorItemObject.ItemType.Clone,
            "hazards" => EditorItemObject.ItemType.Hazard,
            "level_mechanics" => EditorItemObject.ItemType.LevelMechanic,
            "on_off_assets" => EditorItemObject.ItemType.OnOff,
            "npcs" => EditorItemObject.ItemType.NPC,
            _ => EditorItemObject.ItemType.Null
        };
    }

    public IEnumerable<Node2D> PlaceableObjects()
    {
        foreach (Node node in GetTree().GetNodesInGroup(placeableGroup))
        {
            if (node is Node2D obj) yield return obj;
        }
    }

    public List<Node2D> ObjectsAtCell(Vector2I c)
    {
        var list = new List<Node2D>();

        foreach (var obj in PlaceableObjects())
        {
            if (GetObjectCell(obj) == c) list.Add(obj);
        }

        return list;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> QueryObjectsAt(Vector2 worldPos)
    {
        var query = new PhysicsPointQueryParameters2D
        {
            Position = worldPos,
            CollideWithAreas = true,
            CollideWithBodies = true
        };

        return GetWorld2D().DirectSpaceState.IntersectPoint(query);
    }

    /// <summary>True if a tile or any object already occupies the hovered cell.</summary>
    public bool IsHoveredCellBlocked()
    {
        return tileMap.GetCellSourceId(cell) != -1 || ObjectsAtCell(cell).Count > 0 || QueryObjectsAt(snappedWorld).Count > 0;
    }

    // ------------------------------------------------------------------
    // Selection filter
    // ------------------------------------------------------------------
    public bool TilesPassFilter => filter is FilterMode.All or FilterMode.Tiles;

    public bool PassesFilter(Node2D obj)
    {
        var type = GetObjectType(obj);

        return filter switch
        {
            FilterMode.All => true,
            FilterMode.Tiles => false,
            FilterMode.Clones => type == EditorItemObject.ItemType.Clone,
            FilterMode.Enemies => type == EditorItemObject.ItemType.Enemy,
            FilterMode.Hazards => type == EditorItemObject.ItemType.Hazard,
            FilterMode.LevelMechanics => type is EditorItemObject.ItemType.LevelMechanic
                                              or EditorItemObject.ItemType.TileObj,
            FilterMode.OnOff => type == EditorItemObject.ItemType.OnOff,
            FilterMode.NPCs => type == EditorItemObject.ItemType.NPC,
            _ => true
        };
    }

    public void SetFilterMode(FilterMode mode) => currentFilterMode = mode;

    static string FilterLabel(FilterMode mode) => mode switch
    {
        FilterMode.All => "All",
        FilterMode.Clones => "Clones",
        FilterMode.Enemies => "Enemies",
        FilterMode.Hazards => "Hazards",
        FilterMode.Tiles => "Tiles",
        FilterMode.LevelMechanics => "Level Mechanics",
        FilterMode.OnOff => "On/Off Blocks",
        FilterMode.NPCs => "NPCs",
        _ => mode.ToString()
    };

    // ------------------------------------------------------------------
    // Actions (all undoable)
    // ------------------------------------------------------------------
    public void Undo() => history.Undo();
    public void Redo() => history.Redo();

    public void ClearHistory() => history.Clear();

    public void PlaceCurrentItem()
    {
        if (!hasPlaceableItem) return;

        ItemPlaced?.Invoke();

        if (itemType == EditorItemObject.ItemType.Tile)
        {
            var tile = (EditorTileObject)itemObject;

            var before = TileSnapshot.Capture(tileMap, cell);
            var after = new TileSnapshot(tile.tileID, tile.GetAtlasCoords(), 0);
            if (before == after) return;

            var command = new TileChangeCommand(tileMap);
            command.Set(cell, before, after);
            history.Execute(command);
            return;
        }

        if (currentItem == null) return;

        string destination = itemObject.GetGroupDestination();
        Node2D parent = GetTree().CurrentScene.GetNodeOrNull<Node2D>($"editor/main/{levelRoot}/{destination}");

        if (parent == null)
        {
            GD.PushWarning($"Can't place item: no node at editor/main/{levelRoot}/{destination}");
            return;
        }

        Node2D node = currentItem.Instantiate<Node2D>();
        node.AddToGroup(placeableGroup);
        node.SetMeta(metaCell, cell);
        node.SetMeta(metaType, (int)itemType);

        // The command sets GlobalPosition AFTER adding the node, so a container that is offset
        // or scaled doesn't shift the item.
        Vector2 globalPos = snappedWorld + halfTileDown + new Vector2(0, itemObject.cursorOffset.Y);
        history.Execute(ObjectAttachCommand.Add(parent, node, globalPos));
    }

    public void EraseAt(Vector2I targetCell, Vector2 worldPos)
    {
        var commands = new List<EditorCommand>();

        var before = TileSnapshot.Capture(tileMap, targetCell);

        if (before != TileSnapshot.Empty)
        {
            var tileCommand = new TileChangeCommand(tileMap);
            tileCommand.Set(targetCell, before, TileSnapshot.Empty);
            commands.Add(tileCommand);
        }

        var victims = new HashSet<Node2D>(ObjectsAtCell(targetCell));

        // Also anything whose collider is under the cursor.
        foreach (var result in QueryObjectsAt(worldPos))
        {
            Node current = result["collider"].As<Node>();

            while (current != null)
            {
                if (current.IsInGroup(placeableGroup))
                {
                    if (current is Node2D obj) victims.Add(obj);
                    break;
                }

                current = current.GetParent();
            }
        }

        if (victims.Count > 0)
        {
            commands.Add(ObjectAttachCommand.Remove(victims));
        }

        if (commands.Count == 0) return;

        history.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(commands));
    }

    public void DeleteSelection()
    {
        if (selection.isEmpty) return;

        var tiles = selection.tiles.ToList();
        var objects = selection.objects.Where(GodotObject.IsInstanceValid).ToList();
        var commands = new List<EditorCommand>();

        if (tiles.Count > 0)
        {
            var tileCommand = new TileChangeCommand(tileMap);

            foreach (var c in tiles)
            {
                tileCommand.Set(c, TileSnapshot.Capture(tileMap, c), TileSnapshot.Empty);
            }

            commands.Add(tileCommand);
        }

        if (objects.Count > 0)
        {
            commands.Add(ObjectAttachCommand.Remove(objects));
        }

        var composite = new CompositeCommand(commands);

        // Undo brings the selection back along with the items.
        composite.OnApplied = undo =>
        {
            if (undo)
            {
                selection.Set(tiles, objects);
            }
            else
            {
                selection.Clear();
            }
        };

        history.Execute(composite);
    }

    // ------------------------------------------------------------------
    // Context menu hooks
    // ------------------------------------------------------------------

    /// <summary>
    /// Add item-specific right-click options here (or implement IEditorContextProvider on the item's script).
    /// Use AddUndoable / AddUndoablePerObject so the change can be undone.
    /// </summary>
    void RegisterItemContextOptions()
    {
        EditorContextRegistry.Clear();

        // Example: flip every selected on/off block. Flipping twice restores it, so do and undo are the same.
        //
        // EditorContextRegistry.Register(EditorItemObject.ItemType.OnOff, menu =>
        // {
        //     menu.AddUndoablePerObject("Flip Horizontally",
        //         n => n.Scale = new Vector2(-n.Scale.X, n.Scale.Y),
        //         n => n.Scale = new Vector2(-n.Scale.X, n.Scale.Y));
        // });
    }

    /// <summary>Call when any editor popup closes so the click that closed it isn't also used on the level.</summary>
    public void MarkUiInteraction() => uiBlockUntilFrame = Engine.GetProcessFrames() + 2;

    // ------------------------------------------------------------------
    // Overlay (selection highlight + tool previews, drawn in world space)
    // ------------------------------------------------------------------
    public void RedrawOverlay() => overlay?.QueueRedraw();

    void CreateOverlay()
    {
        overlay = new Node2D
        {
            Name = "EditorOverlay",
            TopLevel = true, // independent of this node's position, which follows the cursor
            ZIndex = 100,
            ZAsRelative = false
        };

        overlay.Draw += DrawOverlay;
        AddChild(overlay);
    }

    void DrawOverlay()
    {
        if (!editorActive || selection == null) return;

        var tileFill = new Color(0.3f, 0.6f, 1f, 0.18f);
        var tileLine = new Color(0.3f, 0.6f, 1f, 0.9f);
        var objectLine = new Color(1f, 0.7f, 0.2f, 0.95f);

        foreach (var c in selection.tiles)
        {
            Rect2 rect = CellRect(c);
            overlay.DrawRect(rect, tileFill, filled: true);
            overlay.DrawRect(rect, tileLine, filled: false, width: 1f);
        }

        foreach (var obj in selection.objects)
        {
            if (!GodotObject.IsInstanceValid(obj))
            {
                continue;
            }

            overlay.DrawRect(CellRect(GetObjectCell(obj)), objectLine, filled: false, width: 1.5f);
        }

        activeTool?.Draw(overlay);
    }

    void OnSelectionChanged()
    {
        RedrawOverlay();
        SelectionChanged?.Invoke();
    }

    void OnHistoryChanged()
    {
        selection?.Prune();
        RedrawOverlay();
        HistoryChanged?.Invoke();
    }

    // ------------------------------------------------------------------
    // Built-in filter dropdown
    // ------------------------------------------------------------------
    void CreateFilterUi()
    {
        filterLayer = new CanvasLayer { Layer = 20, Visible = false };

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(new Label { Text = "Select:" });

        filterOption = new OptionButton { FocusMode = Control.FocusModeEnum.None };

        foreach (var mode in Enum.GetValues<FilterMode>())
        {
            filterOption.AddItem(FilterLabel(mode), (int)mode);
        }

        filterOption.Selected = filterOption.GetItemIndex((int)filter);
        filterOption.ItemSelected += index => SetFilterMode((FilterMode)filterOption.GetItemId((int)index));
        filterOption.GetPopup().VisibilityChanged += MarkUiInteraction;

        row.AddChild(filterOption);
        panel.AddChild(row);
        filterLayer.AddChild(panel);
        AddChild(filterLayer);

        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 8);
        panel.GrowHorizontal = Control.GrowDirection.Begin;

        panel.MouseEntered += () => overOwnUi = true;
        panel.MouseExited += () => overOwnUi = false;
    }

    void SyncFilterUi()
    {
        if (filterOption == null) return;

        int index = filterOption.GetItemIndex((int)filter);
        if (filterOption.Selected != index) filterOption.Selected = index;
    }

    // ------------------------------------------------------------------
    // Current item / cursor sprite
    // ------------------------------------------------------------------
    public void SetEditorItem(EditorItemObject editorItem)
    {
        if (editorItem == null)
        {
            return;
        }

        itemObject = editorItem;
        itemType = editorItem.itemType;

        if (editorItem.itemType == EditorItemObject.ItemType.Tile)
        {
            var tileObject = (EditorTileObject)editorItem;
            SetCursorTexture(tileObject.GetTileTexture());
            SetCurrentTile(tileObject.Texture);
        }
        else
        {
            SetCursorTexture(editorItem.Texture, editorItem.FlipH);

            cursorSprite.Material = editorItem.itemType == EditorItemObject.ItemType.Clone ? editorItem.Material : null;

            SetCurrentScene(editorItem.thisScene);
        }

        cursorSprite.Modulate = new Color(1, 1, 1, 0.5f);
    }

    /// <summary>Re-applies the selected item to the cursor sprite (used when entering Place mode).</summary>
    public void RefreshCursorItem()
    {
        if (itemObject != null)
        {
            SetEditorItem(itemObject);
        }
        else
        {
            SetCursorTexture(null);
        }
    }

    public void ClearEditorItem()
    {
        SetCursorTexture(null);
        itemObject = null;
        itemType = EditorItemObject.ItemType.Null;
        currentItem = null;
        currentTile = null;
    }

    public void SetCursorTexture(Texture2D texture, bool flipH = false)
    {
        cursorSprite.Texture = texture;
        cursorSprite.FlipH = flipH;
    }

    public void SetCurrentTile(Texture2D tile)
    {
        currentItem = null;
        currentTile = tile;
    }

    void SetCurrentScene(PackedScene newScene)
    {
        currentTile = null;
        currentItem = newScene;
    }
}