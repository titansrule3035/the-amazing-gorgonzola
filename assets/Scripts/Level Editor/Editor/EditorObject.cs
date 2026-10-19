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
    public const string PlaceableGroup = "editor_placeable";
    public const string MetaCell = "editor_cell";       // Vector2I: the cell an object was placed in
    public const string MetaType = "editor_item_type";  // int: EditorItemObject.ItemType
    const string LevelRoot = "level/level_assets";

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
    [Export] public PackedScene CurrentItem = null;
    [Export] public Texture2D CurrentTile = null;
    [Export] public Texture2D EraserTexture;
    [Export] public CursorMode lastCursorMode = CursorMode.Null;
    [Export] public int MaxUndoSteps = 200;
    [Export] public bool BuildFilterUi = true; // adds a small "Select:" dropdown (top right) while the editor is active

    CursorMode _mode = CursorMode.Place;

    /// <summary>Setting this (from UI, other scripts, etc.) properly switches tools.</summary>
    [Export]
    public CursorMode currentCursorMode
    {
        get => _mode;
        set => SetCursorMode(value);
    }

    FilterMode _filter = FilterMode.All;

    /// <summary>Which kinds of things selecting and grabbing can pick up.</summary>
    [Export]
    public FilterMode currentFilterMode
    {
        get => _filter;
        set
        {
            _filter = value;
            SyncFilterUi();
            FilterChanged?.Invoke(value);
        }
    }

    // ------------------------------------------------------------------
    // Shared state
    // ------------------------------------------------------------------
    public Sprite2D CursorSprite { get; private set; }
    public TileMapLayer TileMap { get; private set; }
    public EditorSelection Selection { get; private set; }
    public EditorHistory History { get; private set; }
    public string levelName { get; set; } = "Untitled";

    public EditorItemObject itemObject;
    public EditorItemObject.ItemType itemType;
    public bool hideCursor = false;

    // Kept for older scripts that read these directly.
    public HashSet<Vector2I> selectedTiles => Selection.Tiles;
    public HashSet<Node2D> selectedObjects => Selection.Objects;

    public Action? ItemPlaced;
    public Action? SelectionChanged;
    public Action? HistoryChanged;
    public event Action<FilterMode>? FilterChanged;
    public event Action? OnSaveComplete;

    // ------------------------------------------------------------------
    // Convenience accessors
    // ------------------------------------------------------------------

    /// <summary>The single "is the editor live" check. Change it here and everything follows.</summary>
    public bool EditorActive => GetTree().Paused;

    public bool PrimaryHeld => Input.IsActionPressed("mb_left");

    /// <summary>Editor is live, the mouse is over the level, and no popup or editor UI is eating the click.</summary>
    public bool CanInteract =>
        EditorActive
        && canPlace
        && !_overOwnUi
        && Engine.GetProcessFrames() > _uiBlockUntilFrame
        && !(_contextMenu?.Menu.Visible ?? false);

    /// <summary>Offset from a tile's center to its bottom edge.</summary>
    public Vector2 HalfTileDown => new(0, TileMap.TileSet.TileSize.Y / 2f);

    public bool HasPlaceableItem =>
        itemObject != null
        && itemType != EditorItemObject.ItemType.Null
        && (CurrentItem != null || CurrentTile != null);

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------
    Dictionary<CursorMode, EditorTool> _tools;
    EditorContextMenu _contextMenu;
    Node2D _overlay;
    CanvasLayer _filterLayer;
    OptionButton _filterOption;

    bool _ctrlOverride;
    bool _wasActive;
    bool _overOwnUi;
    ulong _uiBlockUntilFrame;

    static EditorObject instance;

    EditorTool ActiveTool => _tools != null && _tools.TryGetValue(_mode, out var tool) ? tool : null;

    // ------------------------------------------------------------------
    // Tools / modes
    // ------------------------------------------------------------------
    void BuildTools()
    {
        _tools = new();
        Register(new MouseTool(this));
        Register(new MoveTool(this));
        Register(new SelectTool(this));
        Register(new PlaceTool(this));
        Register(new EraserTool(this));
        Register(new CopyTool(this));
        Register(new DragCameraTool(this));
    }

    void Register(EditorTool tool) => _tools[tool.Mode] = tool;

    /// <summary>Public mode switch. Cancels any pending Ctrl override.</summary>
    public void SetCursorMode(CursorMode mode)
    {
        _ctrlOverride = false;
        SwitchMode(mode);
    }

    void SwitchMode(CursorMode mode)
    {
        ActiveTool?.Exit();
        _mode = mode;
        ActiveTool?.Enter();
        RedrawOverlay();
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

        ProcessMode = ProcessModeEnum.Always; // keep running while the tree is paused

        CursorSprite = GetNode<Sprite2D>("Sprite");
        level = GetParent().GetNode<Node2D>("level");
        TileMap = level.GetNode<TileMapLayer>("tiles/Foreground");

        History = new EditorHistory(MaxUndoSteps);
        History.Changed += OnHistoryChanged;

        Selection = new EditorSelection(this);
        Selection.Changed += OnSelectionChanged;

        CreateOverlay();

        _contextMenu = new EditorContextMenu(this);
        AddChild(_contextMenu.Menu);

        if (BuildFilterUi)
        {
            CreateFilterUi();
        }

        RegisterItemContextOptions();
        BuildTools();
        ActiveTool?.Enter();
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

    /// <summary>Runs `proceed` immediately if clean, otherwise asks Save / Discard / Cancel first.</summary>
    void RequestAction(Action proceed)
    {
        if (!History.IsDirty) { proceed(); return; }

        OnSaveComplete = proceed;
        LevelEditorMain editorMain = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main");

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
        History?.Clear();
    }

    public override void _Process(double delta)
    {
        bool active = EditorActive;

        if (active != _wasActive)
        {
            _wasActive = active;
            OnActiveChanged(active);
        }

        UpdateMouseCell();
        HandleModeHotkeys();

        var tool = ActiveTool;

        if (tool == null)
        {
            CursorSprite.Visible = false;
            return;
        }

        tool.UpdateCursor();

        if (active)
        {
            tool.Update(delta);
        }

        hideCursor = !(active && tool.CursorVisible);
        CursorSprite.Visible = !hideCursor;

        GetWindow().Title = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main").levelName + (EditorObject.GetInstance().History.IsDirty ? "* (Unsaved)" : "") + " - TAG Level Editor";
    
        if (History.IsDirty)
        {
            GetTree().AutoAcceptQuit = false;
        }
        else
        {
            GetTree().AutoAcceptQuit = true;
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!EditorActive || @event is not InputEventKey { Pressed: true } key) return;

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
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) return;
        if (!CanInteract) return;
        if (_mode is CursorMode.Mouse or CursorMode.DragCamera) return;
        if (ActiveTool?.IsBusy == true) return;

        _contextMenu.Open();
        GetViewport().SetInputAsHandled();
    }

    void OnActiveChanged(bool active)
    {
        _overlay.Visible = active;

        if (_filterLayer != null)
        {
            _filterLayer.Visible = active;
        }

        if (!active)
        {
            _overOwnUi = false;
            ActiveTool?.OnEditorDeactivated();
        }

        RedrawOverlay();
    }

    // ------------------------------------------------------------------
    // Per-frame helpers
    // ------------------------------------------------------------------
    void UpdateMouseCell()
    {
        mouseGlobal = GetGlobalMousePosition();
        local = TileMap.ToLocal(mouseGlobal);
        cell = TileMap.LocalToMap(local);
        snappedWorld = TileMap.ToGlobal(TileMap.MapToLocal(cell));
    }

    void HandleModeHotkeys()
    {
        foreach (var (action, mode, needsEditor) in Hotkeys)
        {
            if (needsEditor && !EditorActive) continue;

            if (Input.IsActionJustPressed(action))
            {
                SetCursorMode(mode);
            }
        }

        // Ctrl temporarily becomes the plain mouse, then restores the previous mode.
        // Tools can opt out (Select uses Ctrl for "add to selection", and drags must not be interrupted).
        if (Input.IsActionJustPressed("ctrl")
            && _mode != CursorMode.Mouse
            && ActiveTool is { AllowCtrlMouseOverride: true })
        {
            lastCursorMode = _mode;
            _ctrlOverride = true;
            SwitchMode(CursorMode.Mouse);
        }

        if (Input.IsActionJustReleased("ctrl") && _ctrlOverride)
        {
            _ctrlOverride = false;

            if (lastCursorMode != CursorMode.Null)
            {
                SwitchMode(lastCursorMode);
            }
        }
    }

    // ------------------------------------------------------------------
    // Grid / object helpers
    // ------------------------------------------------------------------
    public Vector2 CellToWorld(Vector2I c) => TileMap.ToGlobal(TileMap.MapToLocal(c));

    public Vector2I WorldToCell(Vector2 world) => TileMap.LocalToMap(TileMap.ToLocal(world));

    /// <summary>World-space rectangle covering one grid cell.</summary>
    public Rect2 CellRect(Vector2I c)
    {
        Vector2 size = TileMap.TileSet.TileSize;
        return new Rect2(CellToWorld(c) - size / 2f, size);
    }

    /// <summary>
    /// The cell a placed object belongs to. Uses the cell stored when it was placed or moved.
    /// (An object's origin sits on a cell's bottom edge, so deriving the cell from position alone is unreliable.)
    /// Objects without that data fall back to "one half-tile above the origin".
    /// </summary>
    public Vector2I GetObjectCell(Node2D obj)
    {
        if (obj.HasMeta(MetaCell))
        {
            return obj.GetMeta(MetaCell).AsVector2I();
        }

        return WorldToCell(obj.GlobalPosition - HalfTileDown);
    }

    public EditorItemObject.ItemType GetObjectType(Node2D obj)
    {
        if (obj.HasMeta(MetaType))
        {
            return (EditorItemObject.ItemType)obj.GetMeta(MetaType).AsInt32();
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
        foreach (Node node in GetTree().GetNodesInGroup(PlaceableGroup))
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
        return TileMap.GetCellSourceId(cell) != -1
            || ObjectsAtCell(cell).Count > 0
            || QueryObjectsAt(snappedWorld).Count > 0;
    }

    // ------------------------------------------------------------------
    // Selection filter
    // ------------------------------------------------------------------
    public bool TilesPassFilter => _filter is FilterMode.All or FilterMode.Tiles;

    public bool PassesFilter(Node2D obj)
    {
        var type = GetObjectType(obj);

        return _filter switch
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
    public void Undo() => History.Undo();
    public void Redo() => History.Redo();

    public void ClearHistory() => History.Clear();

    public void PlaceCurrentItem()
    {
        if (!HasPlaceableItem) return;

        ItemPlaced?.Invoke();

        if (itemType == EditorItemObject.ItemType.Tile)
        {
            var tile = (EditorTileObject)itemObject;

            var before = TileSnapshot.Capture(TileMap, cell);
            var after = new TileSnapshot(tile.tileID, tile.GetAtlasCoords(), 0);
            if (before == after) return;

            var command = new TileChangeCommand(TileMap);
            command.Set(cell, before, after);
            History.Execute(command);
            return;
        }

        if (CurrentItem == null) return;

        string destination = itemObject.GetGroupDestination();
        Node2D parent = GetTree().CurrentScene
            .GetNodeOrNull<Node2D>($"editor/main/{LevelRoot}/{destination}");

        if (parent == null)
        {
            GD.PushWarning($"Can't place item: no node at editor/main/{LevelRoot}/{destination}");
            return;
        }

        Node2D node = CurrentItem.Instantiate<Node2D>();
        node.AddToGroup(PlaceableGroup);
        node.SetMeta(MetaCell, cell);
        node.SetMeta(MetaType, (int)itemType);

        // The command sets GlobalPosition AFTER adding the node, so a container that is offset
        // or scaled doesn't shift the item.
        Vector2 globalPos = snappedWorld + HalfTileDown + new Vector2(0, itemObject.cursorOffset.Y);
        History.Execute(ObjectAttachCommand.Add(parent, node, globalPos));
    }

    public void EraseAt(Vector2I targetCell, Vector2 worldPos)
    {
        var commands = new List<EditorCommand>();

        var before = TileSnapshot.Capture(TileMap, targetCell);

        if (before != TileSnapshot.Empty)
        {
            var tileCommand = new TileChangeCommand(TileMap);
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
                if (current.IsInGroup(PlaceableGroup))
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

        History.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(commands));
    }

    public void DeleteSelection()
    {
        if (Selection.IsEmpty) return;

        var tiles = Selection.Tiles.ToList();
        var objects = Selection.Objects.Where(GodotObject.IsInstanceValid).ToList();
        var commands = new List<EditorCommand>();

        if (tiles.Count > 0)
        {
            var tileCommand = new TileChangeCommand(TileMap);

            foreach (var c in tiles)
            {
                tileCommand.Set(c, TileSnapshot.Capture(TileMap, c), TileSnapshot.Empty);
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
            if (undo) Selection.Set(tiles, objects);
            else Selection.Clear();
        };

        History.Execute(composite);
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
    public void MarkUiInteraction() => _uiBlockUntilFrame = Engine.GetProcessFrames() + 2;

    // ------------------------------------------------------------------
    // Overlay (selection highlight + tool previews, drawn in world space)
    // ------------------------------------------------------------------
    public void RedrawOverlay() => _overlay?.QueueRedraw();

    void CreateOverlay()
    {
        _overlay = new Node2D
        {
            Name = "EditorOverlay",
            TopLevel = true, // independent of this node's position, which follows the cursor
            ZIndex = 100,
            ZAsRelative = false
        };

        _overlay.Draw += DrawOverlay;
        AddChild(_overlay);
    }

    void DrawOverlay()
    {
        if (!EditorActive || Selection == null) return;

        var tileFill = new Color(0.3f, 0.6f, 1f, 0.18f);
        var tileLine = new Color(0.3f, 0.6f, 1f, 0.9f);
        var objectLine = new Color(1f, 0.7f, 0.2f, 0.95f);

        foreach (var c in Selection.Tiles)
        {
            Rect2 rect = CellRect(c);
            _overlay.DrawRect(rect, tileFill, filled: true);
            _overlay.DrawRect(rect, tileLine, filled: false, width: 1f);
        }

        foreach (var obj in Selection.Objects)
        {
            if (!GodotObject.IsInstanceValid(obj)) continue;
            _overlay.DrawRect(CellRect(GetObjectCell(obj)), objectLine, filled: false, width: 1.5f);
        }

        ActiveTool?.Draw(_overlay);
    }

    void OnSelectionChanged()
    {
        RedrawOverlay();
        SelectionChanged?.Invoke();
    }

    void OnHistoryChanged()
    {
        Selection?.Prune();
        RedrawOverlay();
        HistoryChanged?.Invoke();
    }

    // ------------------------------------------------------------------
    // Built-in filter dropdown
    // ------------------------------------------------------------------
    void CreateFilterUi()
    {
        _filterLayer = new CanvasLayer { Layer = 20, Visible = false };

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(new Label { Text = "Select:" });

        _filterOption = new OptionButton { FocusMode = Control.FocusModeEnum.None };

        foreach (var mode in Enum.GetValues<FilterMode>())
        {
            _filterOption.AddItem(FilterLabel(mode), (int)mode);
        }

        _filterOption.Selected = _filterOption.GetItemIndex((int)_filter);
        _filterOption.ItemSelected += index => SetFilterMode((FilterMode)_filterOption.GetItemId((int)index));
        _filterOption.GetPopup().VisibilityChanged += MarkUiInteraction;

        row.AddChild(_filterOption);
        panel.AddChild(row);
        _filterLayer.AddChild(panel);
        AddChild(_filterLayer);

        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 8);
        panel.GrowHorizontal = Control.GrowDirection.Begin;

        panel.MouseEntered += () => _overOwnUi = true;
        panel.MouseExited += () => _overOwnUi = false;
    }

    void SyncFilterUi()
    {
        if (_filterOption == null) return;

        int index = _filterOption.GetItemIndex((int)_filter);
        if (_filterOption.Selected != index) _filterOption.Selected = index;
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

            CursorSprite.Material = editorItem.itemType == EditorItemObject.ItemType.Clone ? editorItem.Material : null;

            SetCurrentScene(editorItem.ThisScene);
        }

        CursorSprite.Modulate = new Color(1, 1, 1, 0.5f);
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
        CurrentItem = null;
        CurrentTile = null;
    }

    public void SetCursorTexture(Texture2D texture, bool flipH = false)
    {
        CursorSprite.Texture = texture;
        CursorSprite.FlipH = flipH;
    }

    public void SetCurrentTile(Texture2D tile)
    {
        CurrentItem = null;
        CurrentTile = tile;
    }

    void SetCurrentScene(PackedScene newScene)
    {
        CurrentTile = null;
        CurrentItem = newScene;
    }
}