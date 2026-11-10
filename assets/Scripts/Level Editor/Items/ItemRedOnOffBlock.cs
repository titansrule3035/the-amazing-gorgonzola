using Godot;
using System;

public partial class ItemRedOnOffBlock : EditorItemObject
{
    AnimationPlayer animationPlayer;

    public override void _Ready()
    {
        animationPlayer = GetNode<AnimationPlayer>("TextureAnimationPlayer");

        OnStateChanged(OnOffManager.on);

        OnOffManager.OnStateChanged += OnStateChanged;

        base._Ready();
    }

    public override void _Process(double delta)
    {
        if (!GetTree().Paused)
        {
            DisableItem();
        }

        base._Process(delta);
    }

    void OnStateChanged(bool state)
    {
        animationPlayer.Play(state ? "open" : "close");
    }

    public override void _ExitTree()
    {
        OnOffManager.OnStateChanged -= OnStateChanged;
    }
}
