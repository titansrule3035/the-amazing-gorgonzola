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

    void OnStateChanged(bool state)
    {
        animationPlayer.Play(state ? "open" : "close");
    }
}
