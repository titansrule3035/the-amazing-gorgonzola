using Godot;
using System;

public partial class Door : Node2D
{
    private static Door instance;

    [Export] public bool opened = false;

    private AnimatedSprite2D animatedSprite;
    private AnimationPlayer animPlayer;
    private AnimationTree animTree;

    private Area2D area;
    private bool inRange;

    private readonly string[] animationParams = { "open", "close" };

    private Gorgonzola gorg;
    private bool lastGroundedState;

    public override async void _Ready()
    {
        if (instance != null)
        {
            GD.PrintErr("Another Door instance exists! Deleting this one...");
            QueueFree();
            return;
        }

        instance = this;

        while (GlobalGameManager.GetInstance() == null && IsInsideTree())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        GlobalGameManager.GetInstance().OnFlush += OnFlush;

        animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        animTree = GetNode<AnimationTree>("AnimationTree");

        area = GetNode<Area2D>("Area2D");
        area.BodyEntered += OnAreaEntered;
        area.BodyExited += OnAreaExited;
    }

    private void OnAreaEntered(Node2D body)
    {
        if (body is Gorgonzola)
        {
            inRange = true;
            UpdateIndicator();
        }
    }

    private void OnAreaExited(Node2D body)
    {
        if (body is Gorgonzola)
        {
            inRange = false;
            UpdateIndicator();
        }
    }

    public override void _Process(double delta)
    {
        gorg = Gorgonzola.GetInstance();

        if (gorg == null || GlobalGameManager.GetInstance() == null)
            return;

        if (GlobalGameManager.GetInstance().levelCompleted)
            return;

        bool grounded = gorg.IsOnFloor();

        if (grounded != lastGroundedState)
        {
            lastGroundedState = grounded;
            UpdateIndicator();
        }

        if (opened && inRange && Input.IsActionJustPressed("interact"))
        {
            GlobalGameManager.GetInstance().levelCompleted = true;
            UpdateIndicator();
        }
    }

    public void Open()
    {
        opened = true;
        PlayAnimation("open");
        UpdateIndicator();
    }

    public void Close()
    {
        opened = false;
        PlayAnimation("close");
        UpdateIndicator();
    }

    private void PlayAnimation(string activeParam)
    {
        foreach (string param in animationParams)
            animTree.Set($"parameters/conditions/{param}", param == activeParam);
    }

    public static Door GetInstance() => instance;

    public override void _ExitTree()
    {
        if (instance == this)
            instance = null;

        base._ExitTree();
    }

    private void UpdateIndicator()
    {
        var gm = GlobalGameManager.GetInstance();

        if (gm == null || gorg == null)
            return;

        if (gm.levelCompleted)
        {
            gorg.SetIndicatorVisibility(false);
            return;
        }

        bool shouldShow =
            opened &&
            inRange &&
            gorg.IsOnFloor();

        if (shouldShow)
        {
            gorg.ChangeIndicator("interact");
            gorg.SetIndicatorVisibility(true);
        }
        else
        {
            gorg.SetIndicatorVisibility(false);
        }
    }

    void OnFlush()
    {
        QueueFree();
    }

    private void RefreshIfNeeded()
    {
        if (gorg == null || GlobalGameManager.GetInstance() == null)
            return;

        UpdateIndicator();
    }

    public Vector2 GetColliderPos()
    {
        return area.GlobalPosition;
    }
}