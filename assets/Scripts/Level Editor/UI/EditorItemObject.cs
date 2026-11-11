using Godot;
using System;

/// <summary>
/// One selectable entry in the editor's item palette (a tile, clone, hazard, etc).
/// Clicking it while the editor is active hands it to EditorObject; clicking it again clears the selection.
/// The palette is inert whenever the game is not paused.
/// </summary>
public partial class EditorItemObject : TextureRect
{
    // Keep this order: values are serialized in scenes. Append new types at the end.
    public enum ItemType
    {
        Clone,
        Enemy,
        Tile,
        TileObj,
        NPC,
        Hazard,
        LevelMechanic,
        OnOff,
        Null
    }

    // ------------------------------------------------------------------
    // Config
    // ------------------------------------------------------------------

    const string editorPath = "editor/main/EditorObject";
    const string mainPath = "editor/main";
    const string animDefault = "default";
    const string animSelected = "selected";
    const string animDisabled = "disabled";

    /// <summary>What kind of thing this entry places. Decides the container and the selection filter it matches.</summary>
    [Export] public ItemType itemType;

    /// <summary>The scene to instantiate for non-tile items.</summary>
    [Export] public PackedScene thisScene { get; set; }

    /// <summary>Extra offset (in pixels) added to the placement position. Only Y is used.</summary>
    [Export] public Vector2 cursorOffset = Vector2.Zero;

    public Button button;

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------

    public AnimationPlayer player;

    /// <summary>True while this item is the one the editor is holding. Always false while the game is running.</summary>
    public bool selected = false;

    /// <summary>Locked by level rules. Separate from the idle state, which applies to every item while the game runs.</summary>
    public bool disabled = false;

    /// <summary>Raised with the newly selected item, or null to clear the selection.</summary>
    public static Action<EditorItemObject?>? ChangeTexture;

    LevelEditorMain main;
    EditorObject? editor;
    bool idleStateApplied;

    /// <summary>True while the game is running. The palette is inert in this state.</summary>
    bool IsIdle => IsInstanceValid(this) && !GetTree().Paused;

    /// <summary>The editor cursor node. Cached on first use.</summary>
    protected EditorObject Editor
    {
        get
        {
            if (!IsInstanceValid(editor))
            {
                editor = GetTree().CurrentScene.GetNode<EditorObject>(editorPath);
            }

            return editor!;
        }
    }

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    public override void _Ready()
    {
        // Look everything up before subscribing, so a missing node can't leave half the wiring in place.
        player = GetParent().GetNode<AnimationPlayer>("AnimationPlayer");
        button = GetParent().GetNode<Button>("Button");
        main = GetTree().CurrentScene.GetNode<LevelEditorMain>(mainPath);

        ChangeTexture += SetActiveItem;
        main.OnGamePaused += OnGamePaused;
        button.Pressed += OnPressed;

        MouseDefaultCursorShape = disabled ? Control.CursorShape.Arrow : Control.CursorShape.PointingHand;
    }

    public override void _ExitTree()
    {
        // ChangeTexture is static, so without this, freed items stay subscribed after a scene reload.
        ChangeTexture -= SetActiveItem;

        // The sibling nodes can already be freed (or never found) by the time this runs.
        if (IsInstanceValid(button))
        {
            button.Pressed -= OnPressed;
        }

        if (IsInstanceValid(main))
        {
            main.OnGamePaused -= OnGamePaused;
        }
    }

    public override void _Process(double delta)
    {
        // While the game is running the palette is inert. Apply that once, not every frame.
        if (IsIdle && !idleStateApplied)
        {
            ApplyIdleState();
        }
    }

    void OnGamePaused()
    {
        idleStateApplied = false;

        if (disabled)
        {
            return;
        }

        // _Process cleared `selected` when the game started running, so the editor is the source of truth.
        if (IsInstanceValid(Editor) && Editor.itemObject == this)
        {
            SelectItem();
        }
        else
        {
            EnableItem();
        }
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    void OnPressed()
    {
        if (IsIdle || disabled)
        {
            return;
        }

        UpdateTextures(Texture, FlipH, !selected);
    }

    // ------------------------------------------------------------------
    // Selection
    // ------------------------------------------------------------------

    /// <summary>
    /// Select (selected = true) or clear (selected = false) this item.
    /// The texture and flipH parameters are unused but kept so subclass overrides still compile.
    /// </summary>
    public virtual void UpdateTextures(Texture2D texture, bool flipH, bool selected)
    {
        if (selected)
        {
            SendItemToEditor();
            ChangeTexture?.Invoke(this);
        }
        else
        {
            // Null resets every palette item to "default" and clears the editor's item (see SetActiveItem).
            ChangeTexture?.Invoke(null);
        }
    }

    /// <summary>Hands this item to the editor. Subclasses override this to send a different payload.</summary>
    public virtual void SendItemToEditor()
    {
        Editor.SetEditorItem(this);
    }

    /// <summary>
    /// Reacts to <see cref="ChangeTexture"/>: this item becomes selected only if it is the one announced.
    /// Every palette item receives the call, so the clear for a null item is repeated. It is idempotent.
    /// While the game is running the item stays inert regardless of what was announced.
    /// </summary>
    public void SetActiveItem(EditorItemObject? item)
    {
        if (IsIdle)
        {
            ApplyIdleState();
        }
        else
        {
            selected = !disabled && item == this;
            player.Play(disabled ? animDisabled : selected ? animSelected : animDefault);
        }

        if (item == null)
        {
            Editor.ClearEditorItem();
        }
    }

    // ------------------------------------------------------------------
    // Enable / disable
    // ------------------------------------------------------------------

    /// <summary>
    /// The inert look and behaviour used whenever the game isn't paused.
    /// Does not touch `disabled`, which means "locked by level rules".
    /// </summary>
    void ApplyIdleState()
    {
        idleStateApplied = true;

        player.Play(animDisabled);
        MouseFilter = MouseFilterEnum.Ignore;
        MouseDefaultCursorShape = Control.CursorShape.Arrow;
        button.Disabled = true;
        selected = false;
    }

    /// <summary>Greys the item out and makes it unclickable. A disabled item can't stay selected.</summary>
    public void DisableItem()
    {
        disabled = true;
        selected = false;

        player.Play(animDisabled);
        MouseFilter = MouseFilterEnum.Ignore;
        MouseDefaultCursorShape = Control.CursorShape.Arrow;
        button.Disabled = true;
    }

    /// <summary>Unlocks the item. While the game is running it stays inert until the next pause.</summary>
    public void EnableItem()
    {
        disabled = false;

        if (IsIdle)
        {
            ApplyIdleState();
            return;
        }

        player.Play(animDefault);
        MouseFilter = MouseFilterEnum.Pass;
        MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        button.Disabled = false;
    }

    /// <summary>Same as EnableItem, but lands in the selected state. Used to restore the editor's held item after a pause.</summary>
    void SelectItem()
    {
        disabled = false;

        if (IsIdle)
        {
            ApplyIdleState();
            return;
        }

        player.Play(animSelected);
        MouseFilter = MouseFilterEnum.Pass;
        MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        button.Disabled = false;
        selected = true;
    }

    // ------------------------------------------------------------------
    // Placement
    // ------------------------------------------------------------------

    /// <summary>Name of the node under level_assets that placed instances are parented to.</summary>
    public string GetGroupDestination() => itemType switch
    {
        ItemType.Clone => "clones",
        ItemType.Enemy => "hazards", // change to "enemies" if enemies get their own container
        ItemType.Hazard => "hazards",
        ItemType.TileObj or ItemType.LevelMechanic => "level_mechanics",
        ItemType.OnOff => "on_off_assets",
        ItemType.NPC => "npcs",
        _ => NoDestination()
    };

    string NoDestination()
    {
        GD.PushWarning($"EditorItemObject '{Name}' has no group destination for item type {itemType}.");
        return "";
    }
}