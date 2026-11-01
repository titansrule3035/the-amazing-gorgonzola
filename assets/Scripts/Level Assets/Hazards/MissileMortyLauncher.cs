using Godot;
using System;

public partial class MissileMortyLauncher : Node2D
{
    [Export] PackedScene missileMorty;

    [Export] float shotSpeed;

    Sprite2D sprite;

    string path;

    bool beganShooting = false;

    public override void _Ready()
    {
        path = (((Runtime)GetTree().CurrentScene).editorMode ? "editor" : "game") + "/main/level/level_assets/hazards";

        sprite = GetNode<Sprite2D>("sprite");
    }

    public override void _Process(double delta)
    {
        if (!beganShooting)
        {
            Shoot();
            beganShooting = true;
        }
    }

    async void Shoot()
    {
        await ToSignal(GetTree().CreateTimer(shotSpeed), SceneTreeTimer.SignalName.Timeout);

        Node2D instance = missileMorty.Instantiate<Node2D>();

        Vector2 spriteSize;

        if (sprite != null)
        {
            spriteSize = sprite.Texture.GetSize();
        }
        else
        {
            spriteSize = GetNode<Sprite2D>("sprite").Texture.GetSize();
        }

        instance.GlobalPosition = new(GlobalPosition.X + sprite.Texture.GetSize().X / 2, GlobalPosition.Y - sprite.Texture.GetSize().Y / 2);

        GetTree().CurrentScene.GetNode<Node2D>(path).AddChild(instance);

        Shoot();
    }
}
