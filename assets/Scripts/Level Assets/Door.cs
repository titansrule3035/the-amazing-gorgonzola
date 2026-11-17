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

    private bool lastGroundedState 
    {
        get => Runtime.editorMode ? EditorGameManager.GetInstance().levelCompleted : GlobalGameManager.GetInstance().levelCompleted;
        set;
    }

    bool cacheLevelCompleted = false;

    public override void _Ready()
    {
        if (instance != null)
        {
            QueueFree();
            return;
        }

        instance = this;

        animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");

        area = GetNode<Area2D>("Area2D");
        area.BodyEntered += OnAreaEntered;
        area.BodyExited += OnAreaExited;

        if (Runtime.editorMode)
        {
            GetTree().CurrentScene.GetNode<EditorGameManager>("editor/main/editor_game_manager").RegisterDoor(this);
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
        if (cacheLevelCompleted)
        {
            return;
        }
        Gorgonzola? gorg = Gorgonzola.GetInstance();
        bool grounded = gorg != null ? gorg.IsOnFloor() : false;

        if (grounded != lastGroundedState)
        {
            lastGroundedState = grounded;
            UpdateIndicator();
        }

        if (opened && inRange && Input.IsActionJustPressed("interact") && grounded)
        {
            Enemy.KillAll?.Invoke();
            if (cacheLevelCompleted)
            {
                return;
            }

            cacheLevelCompleted = true;

            if (Runtime.editorMode)
            {
                EditorGameManager.GetInstance().levelCompleted = cacheLevelCompleted;
            }
            else
            {
                GlobalGameManager.GetInstance().levelCompleted = cacheLevelCompleted;
            }


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

        if (Runtime.editorMode == true)
        {
            GetTree().CurrentScene.GetNode<EditorGameManager>("editor/main/editor_game_manager").UnregisterDoor();
        }
    }

    private void UpdateIndicator()
    {
        GlobalGameManager ggm = null;
        EditorGameManager egm = null;
        Gorgonzola gorg = Gorgonzola.GetInstance();

        if (Runtime.editorMode)
        {
            egm = EditorGameManager.GetInstance();
        }
        else
        {
            ggm = GlobalGameManager.GetInstance();
        }

        if (egm == null && ggm == null)
        {
            return;
        }

        if (Gorgonzola.GetInstance() == null)
        {
            return;
        }

        if (cacheLevelCompleted)
        {
            gorg.SetIndicatorVisibility(false);
            return;
        }

        bool shouldShow = opened && inRange && gorg.IsOnFloor();

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
        if (Gorgonzola.GetInstance() == null || GlobalGameManager.GetInstance() == null)
        {
            return;
        }

        UpdateIndicator();
    }

    public Vector2 GetColliderPos()
    {
        return area.GlobalPosition;
    }
}