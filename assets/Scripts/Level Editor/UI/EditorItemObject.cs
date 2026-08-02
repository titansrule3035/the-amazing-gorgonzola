using Godot;
using System;
using System.Runtime.CompilerServices;

public partial class EditorItemObject : TextureRect
{
    public enum ItemType { Clone, Enemy, Tile, TileObj, Hazard, LevelMechanic, Null }
    [Export] public ItemType itemType;
    [Export] public PackedScene ThisScene { get; set; }
    public AnimationPlayer Player;
    public bool selected = false;
    public bool disabled = false;

    public static Action<EditorItemObject> ChangeTexture;

    public override void _Ready()
    {
        Player = GetParent().GetNode<AnimationPlayer>("AnimationPlayer");

        GuiInput += OnGuiInput;

        ChangeTexture += SetActiveItem;

        Main main = GetTree().CurrentScene as Main;

        main.OnGamePaused += OnGamePaused;
    }

    private void OnGamePaused()
    {
        if (!disabled)
        {
            EnableItem();
        }
    }

    public override void _Process(double delta)
    {
        if (!GetTree().Paused)
        {
            Player.Play("disabled");
            MouseFilter = MouseFilterEnum.Ignore;
            selected = false;
        }

        base._Process(delta);
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (GetTree().Paused)
        {
            if (@event.IsActionPressed("mb_left"))
            {
                if (!selected)
                {
                    UpdateTextures(Texture, FlipH, true);
                }
                else
                {
                    UpdateTextures(new(), false, false);
                }
            }
        }
    }

    public virtual void UpdateTextures(Texture2D texture, bool flipH, bool selected)
    {
        Sprite2D objectCursor = GetTree().CurrentScene.GetNode<Sprite2D>("EditorObject/Sprite");
        EditorObject editorObject = GetTree().CurrentScene.GetNode<EditorObject>("EditorObject");

        SendItemToEditor();

        float alpha = 0f;
        if (selected)
        {
            alpha = 0.5f;
        }
        Godot.Color modulate = objectCursor.Modulate;
        modulate = new(modulate.R, modulate.G, modulate.B, alpha);

        objectCursor.Modulate = modulate;

        ChangeTexture.Invoke(this);
    }

    public virtual void SendItemToEditor()
    {
        Sprite2D objectCursor = GetTree().CurrentScene.GetNode<Sprite2D>("EditorObject/Sprite");
        EditorObject editorObject = GetTree().CurrentScene.GetNode<EditorObject>("EditorObject");

        editorObject.SetEditorItem(this);
    }

    public void SetActiveItem(EditorItemObject item)
    {
        if (this == item)
        {
            if (selected)
            {
                Player.Play("default");
                selected = false;
            }
            else
            {
                Player.Play("selected");
                selected = true;
            }
        }
        else
        {
            Player.Play("default");
            selected = false;
        }

        if (disabled)
        {
            Player.Play("disabled");
            selected = false;
        }
    }

    public void DisableItem()
    {
        Player.Play("disabled");
        MouseFilter = MouseFilterEnum.Ignore;
        disabled = true;
        selected = false;
    }

    public void EnableItem()
    {
        Player.Play("default");
        MouseFilter = MouseFilterEnum.Pass;
        disabled = false;
    }

    public string GetGroupDestination()
    {
        // ItemType { Clone, Enemy, Tile, TileObj, LevelMechanic, Hazard, Null }

        string group = "";
        switch (itemType)
        {
            case ItemType.Clone:
                group = "clones";
                break;

            case ItemType.Enemy:
                group = "hazards";
                break;

            case ItemType.TileObj:
                group = "level_mechanics";
                break;

            case ItemType.Hazard:
                group = "hazards";
                break;

            case ItemType.LevelMechanic:
                group = "level_mechanics";
                break;

        }
        return group;
    }
}