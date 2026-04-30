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

    private Vector2 gorgPos;

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

            // Only show indicator if the door is already open
            if (opened)
            {
                // have gorg display his up indicator
            }
        }
    }

    private void OnAreaExited(Node2D body)
    {
        if (body is Gorgonzola)
        {
            //hide gorgs indicator
            inRange = false;
        }
    }

    public override void _Process(double delta)
    {
        if (GlobalGameManager.GetInstance().levelCompleted)
        {
            //hide gorgs indicator
            return;
        }

        // Toggle door with "jump"
        if (Input.IsActionJustPressed("jump"))
        {
            if (!opened)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        // Handle interaction only when door is open and player is in range
        if (opened && inRange)
        {
            gorgPos = Gorgonzola.GetInstance().GlobalPosition;

            // find gorg and only have his up indicator become visible

            if (Input.IsActionJustPressed("up"))
            {
                GlobalGameManager.GetInstance().levelCompleted = true;
            }
        }
    }

    public void Open()
    {
        opened = true;
        PlayAnimation("open");

        // If player is already in range, show indicator once
        if (inRange)
        {
            // show gorgs indicator
        }
    }

    public void Close()
    {
        opened = false;
        PlayAnimation("close");

        // Always clear indicator when closing
        // hide gorgs indicator
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

    void OnFlush()
    {
        QueueFree();
    }
}
