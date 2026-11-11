using Godot;
using System;

public partial class Splash : Node2D
{
    [Export] PackedScene RuntimeScene;

    [Export] float delaySec = 0.5f;

    AnimatedSprite2D logoSprite;

    public async override void _Ready()
    {
        logoSprite = GetNode<AnimatedSprite2D>("animated_sprite_2d");
        logoSprite.AnimationFinished += OnAnimationFinished;

        await ToSignal(GetTree().CreateTimer(delaySec), SceneTreeTimer.SignalName.Timeout);

        logoSprite.Play("default");
    }

    async void OnAnimationFinished()
    {
        Node2D instance = RuntimeScene.Instantiate<Node2D>();

        await ToSignal(GetTree().CreateTimer(delaySec), SceneTreeTimer.SignalName.Timeout);

        GetTree().ChangeSceneToNode(instance);
    }
}
