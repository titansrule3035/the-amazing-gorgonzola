using Godot;
using System;

public partial class Door : Node2D
{
    private static Door instance;

    [Export] public bool opened = false;

    private AnimatedSprite2D animatedSprite;
    [Export] private AnimationPlayer animPlayer;
    [Export] private AnimationTree animTree;

    private Area2D area;
    private bool inRange;

    private string[] animationParams = { "open", "close" };

    private Gorgonzola gorg;
    private bool lastGroundedState;

    public override async void _Ready()
    {
        if (instance != null)
        {
            QueueFree(); 
            return;
        }

        instance = this;

        while (GlobalGameManager.GetInstance() == null && IsInsideTree())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");

        area = GetNode<Area2D>("Area2D");
        area.BodyEntered += OnAreaEntered;
        area.BodyExited += OnAreaExited;

        GlobalGameManager? ggm = GlobalGameManager.GetInstance();

        if (ggm == null)
        {
            ((Main)GetTree().CurrentScene).RegisterDoor(this);
        }
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

        if (opened && inRange && Input.IsActionJustPressed("interact") && grounded)
        {
            GlobalGameManager.GetInstance().levelCompleted = true;
            UpdateIndicator();
        }

        if (Input.IsActionJustPressed("interact"))
        {
            PlayAnimation("open");  
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
        //fix bug here, animation player is null???
        //ok kinda, only triggered when clone touches the key
        //we get a similar bug when a clone tries to flip an on/off switch

        foreach (string param in animationParams)
        {
            GetNode<AnimationTree>("AnimationTree").Set($"parameters/conditions/{param}", param == activeParam);
        }
    }

    public static Door GetInstance()
    {
        return instance;
    }

    public override void _ExitTree()
    {
        instance = null;

        if (GetTree().CurrentScene is Main main)
        {
            main.UnregisterDoor();
        }
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