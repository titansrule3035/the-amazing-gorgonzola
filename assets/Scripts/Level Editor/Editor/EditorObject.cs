using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The node. Mouse-to-cell math, mode switching, hotkeys, shared actions (place, erase, delete), overlay drawing, filter dropdown.
/// Behaviour lives elsewhere:
/// EditorTools.cs - one class per cursor mode
/// EditorSelection.cs - selection, picking, moving
/// EditorHistory.cs - undo/redo and every undoable command
/// EditorContextMenu.cs - right-click menu and its extension points
///
/// To add a mode: add a CursorMode value (append it), write an EditorTool, Register() it in BuildTools(),
/// and optionally add a row to hotkeys.
/// </summary>
public partial class EditorObject : Node2D
{
    // ------------------------------------------------------------------
    // Types
    // ------------------------------------------------------------------

    /// <summary>What the mouse does. Values are exported, so only ever append new ones.</summary>
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

    /// <summary>Which kinds of things selecting and grabbing can pick up.</summary>
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

    /// <summary>Group every editor-placed object belongs to.</summary>
    public const string placeableGroup = "editor_placeable";

    /// <summary>Meta key, Vector2I: the cell an object was placed in.</summary>
    public const string metaCell = "editor_cell";

    /// <summary>Meta key, int: EditorItemObject.ItemType.</summary>
    public const string metaType = "editor_item_type";

    const string levelRoot = "level/level_assets";
    const string editorMainPath = "editor/main";

    // A press and release closer together than this (in world pixels) counts as a click, not a drag.
    const float clickDragThreshold = 5f;

    // Input Map action names for the edit shortcuts. Replace the placeholders with your real action names.
    // Until an action exists in the Input Map, ActionJustPressed() simply returns false.
    const string undoAction = "undo";
    const string redoAction = "redo";
    const string deleteAction = "delete";
    const string ctrlAction = "ctrl";
    const string shiftAction = "shift";
    const string altAction = "alt";

    // Input action -> mode. Add a row here to bind a new hotkey.
    static (string Action, CursorMode Mode, bool NeedsEditorActive)[] hotkeys =
    {
        ("mouse_mouse", CursorMode.Mouse, false),
        ("mouse_move", CursorMode.Move, false),
        ("mouse_select", CursorMode.Select, false),
        ("mouse_place", CursorMode.Place, false),
        ("mouse_eraser", CursorMode.Eraser, false),
        ("mouse_copy", CursorMode.Copy, true),
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
    [Export] public PackedScene? currentItem = null;
    [Export] public Texture2D? currentTile = null;
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
    public EditorItemObject? itemObject;
    public EditorItemObject.ItemType itemType;

    public bool hideCursor
    {
        get => !cursorSprite.Visible;
        set => cursorSprite.Visible = !value;
    }

    /// <summary>Raised after an item was actually placed (not when placement was blocked or a no-op).</summary>
    public Action? ItemPlaced;

    public Action? SelectionChanged;
    public Action? HistoryChanged;
    public event Action<FilterMode>? FilterChanged;

    /// <summary>
    /// The action to run once a pending save finishes (set by <see cref="RequestAction"/>).
    /// Call <see cref="NotifySaveComplete"/> from the save code, and <see cref="CancelPendingAction"/> if the user cancels.
    /// </summary>
    public Action? OnSaveComplete;

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

    /// <summary>True while a mouse stroke or drag is in progress (the active tool says so).</summary>
    public bool toolBusy => activeTool?.isBusy == true;

    // ------------------------------------------------------------------
    // Private state
    // ------------------------------------------------------------------

    Dictionary<CursorMode, EditorTool>? tools;
    EditorContextMenu? contextMenu;
    Node2D? overlay;
    CanvasLayer? filterLayer;
    OptionButton? filterOption;
    LevelEditorMain? editorMain;
    string lastWindowTitle = "";
    bool ctrlOverride;
    bool wasActive;
    bool overOwnUi;
    bool isDuplicate;
    ulong uiBlockUntilFrame;
    MouseButton pressedButton = MouseButton.None;
    Vector2 pressPos;
    bool selectionPressStarted;

    static EditorObject? instance;

    EditorTool? activeTool
    {
        get
        {
            if (tools != null && tools.TryGetValue(mode, out EditorTool? tool))
            {
                return tool;
            }
            return null;
        }
    }

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

    void Register(EditorTool tool)
    {
        tools![tool.mode] = tool;
    }

    /// <summary>Public mode switch. Cancels any pending Ctrl override.</summary>
    public void SetCursorMode(CursorMode newMode)
    {
        ctrlOverride = false;
        SwitchMode(newMode);
    }

    void SwitchMode(CursorMode newMode)
    {
        activeTool?.Exit();
        mode = newMode;
        activeTool?.Enter();
        UpdateMouseCursor();
        RedrawOverlay();
    }

    /// <summary>Tells the runtime which OS cursor to show for the current mode.</summary>
    void UpdateMouseCursor()
    {
        // The exported setter can run while the scene loads, before this node is in the tree.
        if (!IsInsideTree())
        {
            return;
        }
        (GetTree().CurrentScene as Runtime)?.ApplyCursor(mode.ToString());
    }

    // ------------------------------------------------------------------
    // Godot lifecycle
    // ------------------------------------------------------------------

    public override void _Ready()
    {
        if (instance != null && instance != this && IsInstanceValid(instance))
        {
            GD.PrintErr("Error: only one EditorObject allowed, deleting this one...");
            // QueueFree is deferred, so stop this node from running in the meantime.
            isDuplicate = true;
            SetProcess(false);
            SetProcessInput(false);
            SetProcessUnhandledInput(false);
            SetProcessUnhandledKeyInput(false);
            QueueFree();
            return;
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

    public static EditorObject? GetInstance()
    {
        return instance;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && !isDuplicate && history != null)
        {
            RequestAction(() => GetTree().Quit());
        }
    }

    /// <summary>Runs `proceed` immediately if history is clean or if already prompted, otherwise asks first.</summary>
    void RequestAction(Action proceed)
    {
        LevelEditorMain? main = GetEditorMain();
        if (main == null || !history.IsDirty || main.ui.quitMenu.Visible)
        {
            proceed();
            return;
        }
        OnSaveComplete = proceed;
        main.ui.quitMenu.SetMenuVisibility(true);
        main.ui.quitMenu.programMode = true;
        main.ui.blockMouse.MouseFilter = Control.MouseFilterEnum.Stop;
    }

    /// <summary>Call from the save code once the file is written. Runs the pending action (once) and clears it.</summary>
    public void NotifySaveComplete()
    {
        Action? pending = OnSaveComplete;
        OnSaveComplete = null;
        pending?.Invoke();
    }

    /// <summary>Call if the user backs out of the save prompt, so the pending action doesn't fire on a later save.</summary>
    public void CancelPendingAction()
    {
        OnSaveComplete = null;
    }

    public override void _ExitTree()
    {
        if (instance == this)
        {
            instance = null;
        }
        if (isDuplicate)
        {
            return;
        }
        if (history != null)
        {
            // Detach first: nobody downstream (UI, selection) should be told about a teardown.
            history.Changed -= OnHistoryChanged;
            // Frees any objects that are only kept alive by the undo stacks.
            history.Clear();
        }
        if (selection != null)
        {
            selection.Changed -= OnSelectionChanged;
        }
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
        HandleEditHotkeys();
        UpdateWindowState();
        EditorTool? tool = activeTool;
        if (tool == null)
        {
            hideCursor = true;
            return;
        }
        tool.UpdateCursor();
        if (active)
        {
            tool.Update(delta);
        }
        hideCursor = !(active && tool.cursorVisible);
    }

    /// <summary>Window title (with an unsaved marker) and whether closing the window asks first.</summary>
    void UpdateWindowState()
    {
        bool dirty = history.IsDirty;
        GetTree().AutoAcceptQuit = !dirty;
        LevelEditorMain? main = GetEditorMain();
        if (main == null)
        {
            return;
        }
        string title = main.levelName + (dirty ? "* (Unsaved)" : "") + " - TAG Level Editor";
        if (title != lastWindowTitle)
        {
            lastWindowTitle = title;
            GetWindow().Title = title;
        }
    }

    LevelEditorMain? GetEditorMain()
    {
        if (!IsInstanceValid(editorMain))
        {
            editorMain = GetTree().CurrentScene?.GetNodeOrNull<LevelEditorMain>(editorMainPath);
        }
        return editorMain;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton || mouseButton.ButtonIndex != MouseButton.Left)
        {
            return;
        }
        if (mouseButton.Pressed)
        {
            // Only track presses that the editor itself would act on (not clicks on its own UI or popups).
            bool tracking = canInteract;
            pressedButton = tracking ? MouseButton.Left : MouseButton.None;
            pressPos = GetGlobalMousePosition();
            selectionPressStarted = tracking && !selection.isEmpty;
            return;
        }

        // Release. Always reset the press state, even when we bail out below, so it can't go stale.
        bool pressWasTracked = pressedButton == MouseButton.Left && selectionPressStarted;
        Vector2 startPos = pressPos;
        pressedButton = MouseButton.None;
        selectionPressStarted = false;
        if (!pressWasTracked || !editorActive || !canPlace)
        {
            return;
        }

        // Only cancel selection on a click, not a drag.
        Vector2 releasePos = GetGlobalMousePosition();
        if (startPos.DistanceTo(releasePos) >= clickDragThreshold)
        {
            return;
        }
        Vector2I releaseCell = WorldToCell(releasePos);
        bool clickedEmptySpace = ObjectsAtCell(releaseCell).Count == 0 && QueryObjectsAt(releasePos).Count == 0 && tileMap.GetCellSourceId(releaseCell) == -1;
        if (clickedEmptySpace)
        {
            selection.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Hotkey gatekeeps
    // ------------------------------------------------------------------

    /// <summary>
    /// Gatekeep for EVERY hotkey, mode hotkeys included. Return true to swallow the shortcut.
    /// Add your own conditions here.
    /// </summary>
    bool HotkeysBlocked()
    {
        // A text field has focus: typing must not trigger shortcuts.
        Control? focused = GetViewport().GuiGetFocusOwner();
        if (focused is LineEdit or TextEdit)
        {
            return true;
        }
        // A popup or the context menu is open.
        if (contextMenu?.Menu.Visible ?? false)
        {
            return true;
        }
        // PLACEHOLDER: your own global gatekeep (e.g. a rebind screen is open, a dialog is showing).
        // if (Input.IsActionPressed("PLACEHOLDER_gate"))
        // {
        //     return true;
        // }
        return false;
    }

    /// <summary>
    /// Gatekeep for the edit shortcuts (undo, redo, delete). Return true to allow.
    /// These only make sense while the editor is live and no stroke or drag is in progress.
    /// </summary>
    bool EditHotkeysAllowed()
    {
        return editorActive && !toolBusy && !HotkeysBlocked();
    }

    /// <summary>
    /// Input.IsActionJustPressed that tolerates an action that is not in the Input Map yet.
    /// Godot logs an error for unknown actions, which would spam every frame while placeholders are in place.
    /// </summary>
    static bool ActionJustPressed(string action)
    {
        return InputMap.HasAction(action) && Input.IsActionJustPressed(action);
    }

    /// <summary>Undo, redo and delete through the Input Map. Bind the keys (and modifiers) there.</summary>
    void HandleEditHotkeys()
    {
        if (!EditHotkeysAllowed())
        {
            return;
        }

        bool ctrl = InputMap.HasAction(ctrlAction) && Input.IsActionPressed(ctrlAction);

        bool shift = InputMap.HasAction(shiftAction) && Input.IsActionPressed(shiftAction);

        bool alt = InputMap.HasAction(altAction) && Input.IsActionPressed(altAction);

        if (ActionJustPressed(undoAction) && ctrl && !shift && !alt)
        {
            Undo();
        }

        if (ActionJustPressed(redoAction) && ctrl && shift && !alt)
        {
            Redo();
        }

        if (ActionJustPressed(deleteAction) && !ctrl && !shift && !alt)
        {
            DeleteSelection();
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
        if (toolBusy)
        {
            return;
        }
        contextMenu?.Open();
        GetViewport().SetInputAsHandled();
    }

    void OnActiveChanged(bool active)
    {
        if (overlay != null)
        {
            overlay.Visible = active;
        }
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

    bool ModifierHeld()
    {
        return InputMap.HasAction(ctrlAction) && Input.IsActionPressed(ctrlAction) || InputMap.HasAction(shiftAction) && Input.IsActionPressed(shiftAction) || InputMap.HasAction(altAction) && Input.IsActionPressed(altAction);
    }

    bool NoModifiersHeld()
    {
        return !ModifierHeld();
    }

    void HandleModeHotkeys()
    {
        bool blocked = HotkeysBlocked() || !NoModifiersHeld();

        foreach (var (action, hotkeyMode, needsEditorActive) in hotkeys)
        {
            if (blocked || (needsEditorActive && !editorActive))
            {
                continue;
            }

            if (InputMap.HasAction(action) && Input.IsActionJustPressed(action))
            {
                SetCursorMode(hotkeyMode);
            }
        }

        // Ctrl temporarily becomes the plain mouse, then restores the previous mode.
        // Tools can opt out (Select uses Ctrl for "add to selection", and drags must not be interrupted).
        // Only while the editor is live, so Ctrl in gameplay can't flip the tool or the OS cursor.
        if (editorActive && Input.IsActionJustPressed("ctrl") && mode != CursorMode.Mouse && activeTool is { allowCtrlMouseOverride: true })
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

    public Vector2 CellToWorld(Vector2I c)
    {
        return tileMap.ToGlobal(tileMap.MapToLocal(c));
    }

    public Vector2I WorldToCell(Vector2 world)
    {
        return tileMap.LocalToMap(tileMap.ToLocal(world));
    }

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

    /// <summary>
    /// The item type of a placed object. Uses the type stored when it was placed. Objects that weren't
    /// placed this session fall back to their container's name (so an Enemy loaded from disk reads as a Hazard).
    /// </summary>
    public EditorItemObject.ItemType GetObjectType(Node2D obj)
    {
        if (obj.HasMeta(metaType))
        {
            return (EditorItemObject.ItemType)obj.GetMeta(metaType).AsInt32();
        }
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

    /// <summary>Every node in the placeable group that is currently in the tree.</summary>
    public IEnumerable<Node2D> PlaceableObjects()
    {
        foreach (Node node in GetTree().GetNodesInGroup(placeableGroup))
        {
            if (node is Node2D obj)
            {
                yield return obj;
            }
        }
    }

    public List<Node2D> ObjectsAtCell(Vector2I c)
    {
        var list = new List<Node2D>();
        foreach (Node2D obj in PlaceableObjects())
        {
            if (GetObjectCell(obj) == c)
            {
                list.Add(obj);
            }
        }
        return list;
    }

    /// <summary>Physics hits (areas and bodies) at a world position.</summary>
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

    /// <summary>True if the current filter lets selecting and grabbing pick this object up.</summary>
    public bool PassesFilter(Node2D obj)
    {
        EditorItemObject.ItemType type = GetObjectType(obj);
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

    public void SetFilterMode(FilterMode newFilter)
    {
        currentFilterMode = newFilter;
    }

    static string FilterLabel(FilterMode filterMode) => filterMode switch
    {
        FilterMode.All => "All",
        FilterMode.Clones => "Clones",
        FilterMode.Enemies => "Enemies",
        FilterMode.Hazards => "Hazards",
        FilterMode.Tiles => "Tiles",
        FilterMode.LevelMechanics => "Level Mechanics",
        FilterMode.OnOff => "On/Off Blocks",
        FilterMode.NPCs => "NPCs",
        _ => filterMode.ToString()
    };

    // ------------------------------------------------------------------
    // Actions (all undoable)
    // ------------------------------------------------------------------

    public void Undo()
    {
        history.Undo();
    }

    public void Redo()
    {
        history.Redo();
    }

    public void ClearHistory()
    {
        history.Clear();
    }

    /// <summary>Places the current palette item on the hovered cell as one undoable command.</summary>
    public void PlaceCurrentItem()
    {
        if (!hasPlaceableItem)
        {
            return;
        }
        bool placed = itemType == EditorItemObject.ItemType.Tile ? PlaceTile() : PlaceObject();
        if (placed)
        {
            ItemPlaced?.Invoke();
        }
    }

    bool PlaceTile()
    {
        var tile = (EditorTileObject)itemObject!;
        TileSnapshot before = TileSnapshot.Capture(tileMap, cell);
        var after = new TileSnapshot(tile.tileID, tile.GetAtlasCoords(), 0);
        if (before == after)
        {
            return false;
        }
        var command = new TileChangeCommand(tileMap);
        command.Set(cell, before, after);
        history.Execute(command);
        return true;
    }

    bool PlaceObject()
    {
        if (currentItem == null)
        {
            return false;
        }
        string destination = itemObject!.GetGroupDestination();
        if (string.IsNullOrEmpty(destination))
        {
            // An empty path would resolve to level_assets itself and drop the object in the wrong container.
            return false;
        }
        Node2D? parent = GetTree().CurrentScene.GetNodeOrNull<Node2D>($"{editorMainPath}/{levelRoot}/{destination}");
        if (parent == null)
        {
            GD.PushWarning($"Can't place item: no node at {editorMainPath}/{levelRoot}/{destination}");
            return false;
        }
        Node2D node = currentItem.Instantiate<Node2D>();
        node.AddToGroup(placeableGroup);
        node.SetMeta(metaCell, cell);
        node.SetMeta(metaType, (int)itemType);
        // The command sets GlobalPosition AFTER adding the node, so a container that is offset
        // or scaled doesn't shift the item.
        Vector2 globalPos = snappedWorld + halfTileDown + new Vector2(0, itemObject.cursorOffset.Y);
        history.Execute(ObjectAttachCommand.Add(parent, node, globalPos));
        return true;
    }

    /// <summary>Erases the tile in a cell and every placed object in that cell or whose collider is at the world position.</summary>
    public void EraseAt(Vector2I targetCell, Vector2 worldPos)
    {
        var commands = new List<EditorCommand>();
        TileSnapshot before = TileSnapshot.Capture(tileMap, targetCell);
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
            Node? current = result["collider"].As<Node>();
            while (current != null)
            {
                if (current.IsInGroup(placeableGroup))
                {
                    if (current is Node2D obj)
                    {
                        victims.Add(obj);
                    }
                    break;
                }
                current = current.GetParent();
            }
        }
        if (victims.Count > 0)
        {
            commands.Add(ObjectAttachCommand.Remove(victims));
        }
        if (commands.Count == 0)
        {
            return;
        }
        history.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(commands));
    }

    /// <summary>Deletes the selected tiles and objects as one undo step. Undo restores the selection too.</summary>
    public void DeleteSelection()
    {
        if (selection.isEmpty)
        {
            return;
        }
        List<Vector2I> tiles = selection.tiles.ToList();
        List<Node2D> objects = selection.objects.Where(GodotObject.IsInstanceValid).ToList();
        var commands = new List<EditorCommand>();
        if (tiles.Count > 0)
        {
            var tileCommand = new TileChangeCommand(tileMap);
            foreach (Vector2I c in tiles)
            {
                tileCommand.Set(c, TileSnapshot.Capture(tileMap, c), TileSnapshot.Empty);
            }
            commands.Add(tileCommand);
        }
        if (objects.Count > 0)
        {
            commands.Add(ObjectAttachCommand.Remove(objects));
        }
        if (commands.Count == 0)
        {
            // Only freed objects were selected. Nothing to delete, but don't leave a stale selection behind.
            selection.Clear();
            return;
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
    public void MarkUiInteraction()
    {
        uiBlockUntilFrame = Engine.GetProcessFrames() + 2;
    }

    // ------------------------------------------------------------------
    // Overlay (selection highlight + tool previews, drawn in world space)
    // ------------------------------------------------------------------

    public void RedrawOverlay()
    {
        overlay?.QueueRedraw();
    }

    void CreateOverlay()
    {
        var newOverlay = new Node2D
        {
            Name = "EditorOverlay",
            TopLevel = true, // independent of this node's position, which follows the cursor
            ZIndex = 100,
            ZAsRelative = false
        };
        newOverlay.Draw += DrawOverlay;
        overlay = newOverlay;
        AddChild(newOverlay);
    }

    void DrawOverlay()
    {
        if (overlay == null || !editorActive || selection == null)
        {
            return;
        }
        var tileFill = new Color(0.3f, 0.6f, 1f, 0.18f);
        var tileLine = new Color(0.3f, 0.6f, 1f, 0.9f);
        var objectLine = new Color(1f, 0.7f, 0.2f, 0.95f);
        foreach (Vector2I c in selection.tiles)
        {
            Rect2 rect = CellRect(c);
            overlay.DrawRect(rect, tileFill, filled: true);
            overlay.DrawRect(rect, tileLine, filled: false, width: 1f);
        }
        foreach (Node2D obj in selection.objects)
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
        var layer = new CanvasLayer { Layer = 20, Visible = false };
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(new Label { Text = "Select:" });
        var option = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (FilterMode filterMode in Enum.GetValues<FilterMode>())
        {
            option.AddItem(FilterLabel(filterMode), (int)filterMode);
        }
        option.Selected = option.GetItemIndex((int)filter);
        option.ItemSelected += index => SetFilterMode((FilterMode)option.GetItemId((int)index));
        option.GetPopup().VisibilityChanged += MarkUiInteraction;
        row.AddChild(option);
        panel.AddChild(row);
        layer.AddChild(panel);
        AddChild(layer);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 8);
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.MouseEntered += () => overOwnUi = true;
        panel.MouseExited += () => overOwnUi = false;
        filterLayer = layer;
        filterOption = option;
    }

    void SyncFilterUi()
    {
        if (filterOption == null)
        {
            return;
        }
        int index = filterOption.GetItemIndex((int)filter);
        if (filterOption.Selected != index)
        {
            filterOption.Selected = index;
        }
    }

    // ------------------------------------------------------------------
    // Current item / cursor sprite
    // ------------------------------------------------------------------

    /// <summary>Makes a palette entry the current item and shows it on the cursor sprite.</summary>
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
            cursorSprite.Material = null;
            SetCurrentTile(tileObject.Texture);
        }
        else
        {
            SetCursorTexture(editorItem.Texture, editorItem.FlipH);
            // Reset every time, otherwise a clone's material sticks to the cursor when switching to another type.
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

    /// <summary>Forgets the current item and blanks the cursor sprite.</summary>
    public void ClearEditorItem()
    {
        SetCursorTexture(null);
        cursorSprite.Material = null;
        itemObject = null;
        itemType = EditorItemObject.ItemType.Null;
        currentItem = null;
        currentTile = null;
    }

    public void SetCursorTexture(Texture2D? texture, bool flipH = false)
    {
        cursorSprite.Texture = texture;
        cursorSprite.FlipH = flipH;
    }

    public void SetCurrentTile(Texture2D? tile)
    {
        currentItem = null;
        currentTile = tile;
    }

    void SetCurrentScene(PackedScene? newScene)
    {
        currentTile = null;
        currentItem = newScene;
    }
}