using Godot;
using System;

public partial class Splash : Node2D
{
    [Export] PackedScene RuntimeScene;
    [Export] float delaySec = 0.5f;
    [Export] float bordieMoveSpeed;
    [Export] float foregroundFadeSec = 1.2f;

    enum BordieState { Idle, Walking, WaitingForLoopEnd, Sighing, Done }

    Sprite2D backgroundSprite;
    Sprite2D foregroundSprite;
    AnimatedSprite2D logoSprite;
    AnimatedSprite2D bordieSprite;
    AnimationPlayer bordiePlayer;
    AnimationTree bordieTree;

    bool isFading = false;

    Gradient foregroundGradient;
    Tween fadeTween;

    BordieState state = BordieState.Idle;
    float targetXPos;
    double lastWalkPos = 0;
    bool ending = false;

    public async override void _Ready()
    {
        backgroundSprite = GetNode<Sprite2D>("background");

        foregroundSprite = GetNode<Sprite2D>("foreground");
        SetupForegroundGradient();

        logoSprite = GetNode<AnimatedSprite2D>("logo_sprite");
        logoSprite.AnimationFinished += OnLogoAnimated;

        bordieSprite = GetNode<AnimatedSprite2D>("bordie_sprite");

        // Turn the tree off so it can't override the player or fire transitions
        bordieTree = bordieSprite.GetNode<AnimationTree>("anim_tree");
        bordieTree.Active = false;

        bordiePlayer = bordieSprite.GetNode<AnimationPlayer>("anim_player");
        bordiePlayer.AnimationFinished += OnBordieAnimationFinished;

        targetXPos = GetNode<Node2D>("target").GlobalPosition.X;

        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        logoSprite.Play("default");
    }

    // Grabs the gradient from the foreground's texture and duplicates it so
    // changing alpha at runtime never touches a resource shared with other scenes.
    void SetupForegroundGradient()
    {
        switch (foregroundSprite.Texture)
        {
            case GradientTexture1D tex1D:
                foregroundGradient = (Gradient)tex1D.Gradient.Duplicate();
                tex1D.Gradient = foregroundGradient;
                break;
            case GradientTexture2D tex2D:
                foregroundGradient = (Gradient)tex2D.Gradient.Duplicate();
                tex2D.Gradient = foregroundGradient;
                break;
            default:
                break;
        }
    }

    void SetForegroundAlpha(float alpha)
    {
        if (foregroundGradient == null)
        {
            return;
        }

        Color c = foregroundGradient.GetColor(0);
        c.A = alpha;
        foregroundGradient.SetColor(0, c);
    }

    void StartForegroundFade()
    {
        if (!isFading)
        {
            isFading = true;

            float startAlpha = foregroundGradient != null ? foregroundGradient.GetColor(0).A : 0f;

            fadeTween?.Kill();
            fadeTween = CreateTween();
            fadeTween.TweenMethod(Callable.From<float>(SetForegroundAlpha), startAlpha, 1.0f, foregroundFadeSec);
            fadeTween.TweenCallback(Callable.From(() => EndSplash(true)));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        switch (state)
        {
            case BordieState.Walking:
                Vector2 pos = bordieSprite.GlobalPosition;
                if (pos.X > targetXPos)
                {
                    pos.X -= bordieMoveSpeed * (float)delta;
                    bordieSprite.GlobalPosition = pos;
                }
                else
                {
                    lastWalkPos = bordiePlayer.CurrentAnimationPosition;
                    state = BordieState.WaitingForLoopEnd;
                }
                break;

            case BordieState.WaitingForLoopEnd:
                double playPos = bordiePlayer.CurrentAnimationPosition;
                double remaining = bordiePlayer.CurrentAnimationLength - playPos;

                if (remaining <= delta * 2 || playPos < lastWalkPos)
                {
                    state = BordieState.Sighing;
                    bordiePlayer.Play("sigh");
                }
                lastWalkPos = playPos;
                break;
        }
    }

    void OnLogoAnimated()
    {
        state = BordieState.Walking;
        bordiePlayer.Play("walk");
    }

    void OnBordieAnimationFinished(StringName animName)
    {
        if (animName == "sigh" && state == BordieState.Sighing)
        {
            state = BordieState.Done;

            bordiePlayer.Play("end_boil");
            if (!isFading)
            {
                StartForegroundFade();
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion || !@event.IsPressed())
        {
            return;
        }
        if (@event.IsEcho())
        {
            return;
        }

        // Skipped by the player
        StartForegroundFade();
    }

    // ranFully: true when the splash played all the way through, false when skipped
    void EndSplash(bool ranFully)
    {
        if (ending)
        {
            return;
        }
        ending = true;

        fadeTween?.Kill();

        Runtime instance = RuntimeScene.Instantiate<Runtime>();
        instance.ranWithSplash = ranFully;

        if (IsInstanceValid(this))
        {
            GetTree().ChangeSceneToNode(instance);
        }
    }
}