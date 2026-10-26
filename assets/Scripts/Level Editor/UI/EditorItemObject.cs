using Godot;
using System;

/// <summary>
/// One selectable entry in the editor's item palette (a tile, clone, hazard, etc).
/// Clicking it while the editor is active hands it to EditorObject; clicking it again clears the selection.
/// </summary>
public partial class EditorItemObject : TextureRect
{
    // Keep this order: values are serialized in scenes.
    public enum ItemType { Clone, Enemy, Tile, TileObj, NPC, Hazard, LevelMechanic, OnOff, Null }

    // ------------------------------------------------------------------
    // Config
    // ------------------------------------------------------------------
    string editorPath = "editor/main/EditorObject";
    string mainPath = "editor/main";

    string animDefault = "default";
    string animSelected = "selected";
    string animDisabled = "disabled";

    [Export] public ItemType itemType;
    [Export] public PackedScene thisScene { get; set; }
    [Export] public Vector2 cursorOffset = Vector2.Zero;

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------
    public AnimationPlayer player;
    public bool selected = false;
    public bool disabled = false;

    /// <summary>Raised with the newly selected item, or null to clear the selection.</summary>
    public static Action<EditorItemObject>? ChangeTexture;

    LevelEditorMain main;
    EditorObject editor;
    bool idleStateApplied;

    /// <summary>The editor cursor node. Cached on first use.</summary>
    protected EditorObject Editor
    {
        get
        {
            if (!IsInstanceValid(editor))
            {
                editor = GetTree().CurrentScene.GetNode<EditorObject>(editorPath);
            }

            return editor;
        }
    }

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------
    public override void _Ready()
    {
        player = GetParent().GetNode<AnimationPlayer>("AnimationPlayer");

        ChangeTexture += SetActiveItem;

        main = GetTree().CurrentScene.GetNode<LevelEditorMain>(mainPath);
        main.OnGamePaused += OnGamePaused;
    }

    public override void _EnterTree()
    {
        GuiInput += OnGuiInput;
    }

    public override void _ExitTree()
    {
        // ChangeTexture is static, so without this, freed items stay subscribed after a scene reload.
        ChangeTexture -= SetActiveItem;

        GuiInput -= OnGuiInput;

        if (IsInstanceValid(main))
        {
            main.OnGamePaused -= OnGamePaused;
        }
    }

    public override void _Process(double delta)
    {
        if (disabled)
        {
            //((Runtime)GetTree().CurrentScene).ApplyCursor(GetNode<EditorObject>("editor/main/EditorObject").currentCursorMode);
        }
        else
        {
            MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        }

        // While the game is running the palette is inert. Apply that once, not every frame.
        if (GetTree().Paused)
        {
            return;
        }

        if (!idleStateApplied)
        {
            idleStateApplied = true;
            player.Play(animDisabled);
            MouseFilter = MouseFilterEnum.Ignore;
            selected = false;
        }
    }

    void OnGamePaused()
    {
        idleStateApplied = false;

        if (!disabled)
        {
            EnableItem();
        }
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------
    void OnGuiInput(InputEvent @event)
    {
        if (!GetTree().Paused || disabled) return;
        if (!@event.IsActionPressed("mb_left")) return;

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

    public virtual void SendItemToEditor()
    {
        Editor.SetEditorItem(this);
    }

    public void SetActiveItem(EditorItemObject item)
    {
        selected = !disabled && item == this;

        player.Play(disabled ? animDisabled : selected ? animSelected : animDefault);

        // Every palette item receives this call, so the clear is repeated. It is idempotent.
        if (item == null)
        {
            Editor.ClearEditorItem();
        }
    }

    // ------------------------------------------------------------------
    // Enable / disable
    // ------------------------------------------------------------------
    public void DisableItem()
    {
        player.Play(animDisabled);
        MouseFilter = MouseFilterEnum.Ignore;
        disabled = true;
        selected = false;
    }

    public void EnableItem()
    {
        player.Play(animDefault);
        MouseFilter = MouseFilterEnum.Pass;
        disabled = false;
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