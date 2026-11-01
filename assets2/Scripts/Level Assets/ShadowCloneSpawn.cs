using Godot;
using System;

public partial class ShadowCloneSpawn : Node2D
{
    [Export] PackedScene shadowClone;
    [Export] bool huntMode = false;
    AnimatedSprite2D sprite;

    public override void _Ready()
    {
        sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        sprite.AnimationFinished += SpawnShadowClone;
    }

    void SpawnShadowClone()
    {
        ShadowClone instance = shadowClone.Instantiate<ShadowClone>();
        instance.GlobalPosition = GlobalPosition;
        instance.target = Gorgonzola.GetInstance();
        instance.UseChaseMode = huntMode;
        Runtime runtime = (Runtime)GetTree().CurrentScene;
        runtime.GetNode<Node2D>((runtime.editorMode ? "editor" : "game") + "/main/level/level_assets/hazards").AddChild(instance);
        QueueFree();
    }
}
