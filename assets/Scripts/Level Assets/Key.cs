using Godot;
using System;

public partial class Key : Node2D
{
    // Node references
    AnimatedSprite2D animatedSprite2D;
    Area2D area;

    // UI element shown in the HUD when collected
    TextureRect uiElement;

    // Exported editor properties
    [Export] public float uiElementSize;
    [Export] public Vector2 uiElementPos;
    [Export] public bool displayInUI = true;

    // Singleton instance for easy access
    private static Key instance;

    public override void _Ready()
    {
        animatedSprite2D = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        area = GetNode<Area2D>("Area2D");

        area.BodyEntered += OnBodyEntered;

        if (displayInUI)
        {
            uiElement = new();
            GlobalGameManager.GetInstance().GetActiveLevel().GetNode<CanvasLayer>("canvas_layer").AddChild(uiElement);
            uiElement.Name = "key_ui_element";
            uiElement.Texture = animatedSprite2D.SpriteFrames.GetFrameTexture("default", 0);
            uiElement.Modulate = Colors.Black;
            uiElement.TextureFilter = TextureFilterEnum.Nearest;
            uiElement.Size = new(uiElementSize, uiElementSize);

            uiElement.Position = uiElementPos;
        }

        instance = this;

        GlobalGameManager? ggm = GlobalGameManager.GetInstance();
        if (ggm == null)
        {
            ((Main)GetTree().CurrentScene).RegisterKey(this);
        }

        base._Ready();
    }
    private void OpenDoor()
    {
        var door = Door.GetInstance();
        if (door != null)
        {
            door.Open();
        }
    }
    private void OnBodyEntered(Node2D body)
    {
        if (body is BasePlayerController)
        {
            OpenDoor();

            (body as BasePlayerController).hasKey = true;

            QueueFree();
        }
    }
    public override void _ExitTree()
    {
        if (displayInUI)
        {
            uiElement.Modulate = Colors.White;
        }

        ((Main)GetTree().CurrentScene).UnregisterKey();

        base._ExitTree();
    }

    public void BlackenUIElement()
    {
        if (displayInUI)
        {
            uiElement.Modulate = Colors.Black;
        }
    }

    public static Key GetInstance()
    {
        return instance;
    }
}
