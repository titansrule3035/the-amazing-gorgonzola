using Godot;
using System;

public partial class ShadowCloneSpawn : Node2D
{
    [Export] PackedScene shadowClone;
    [Export] bool huntMode = false;
    AnimationPlayer animationPlayer;
    bool startedSpawning = false;

    public override void _Ready()
    {
        animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        animationPlayer.AnimationFinished += SpawnShadowClone;
        
        if (GetTree().Paused)
        {
            animationPlayer.Play("wait");
        }
    }

    public override void _Process(double delta)
    {
        if (GetTree().Paused)
        {
            return;
        }

        if (!startedSpawning)
        {
            startedSpawning = true;
            
            animationPlayer.Play("spawn");
        }
    }

    void SpawnShadowClone(StringName animName)
    {
        if (animName != "spawn")
        {
            return;
        }

        ShadowClone instance = shadowClone.Instantiate<ShadowClone>();
        instance.GlobalPosition = GlobalPosition;
        instance.target = Gorgonzola.GetInstance();
        instance.UseChaseMode = huntMode;
        Runtime runtime = (Runtime)GetTree().CurrentScene;
        runtime.GetNode<Node2D>((runtime._editorMode ? "editor" : "game") + "/main/level/level_assets/hazards").AddChild(instance);
        QueueFree();
    }
}
