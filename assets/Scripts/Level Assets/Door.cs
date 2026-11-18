using Godot;
using System;

public partial class Door : Node2D
{
    private const string EditorGameManagerPath = "editor/main/editor_game_manager";

    private static Door instance;

    [Export] public bool opened = false;

    private AnimatedSprite2D animatedSprite;
    [Export] private AnimationPlayer animPlayer;
    [Export] private AnimationTree animTree;

    private Area2D area;
    private bool inRange;

    private readonly string[] animationParams = { "open", "close" };

    // Last known "player is on floor" value, used to detect changes.
    private bool lastGrounded = false;

    // Set once the player has used the door. Stops all further processing.
    private bool cacheLevelCompleted = false;

    // True for a second Door that freed itself in _Ready because one already exists.
    private bool isDuplicate = false;

    public override void _Ready()
    {
        if (instance != null)
        {
            isDuplicate = true;
            QueueFree();
            return;
        }

        instance = this;

        animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        animPlayer ??= GetNode<AnimationPlayer>("AnimationPlayer");
        animTree ??= GetNode<AnimationTree>("AnimationTree");

        area = GetNode<Area2D>("Area2D");
        area.BodyEntered += OnAreaEntered;
        area.BodyExited += OnAreaExited;

        if (Runtime.editorMode)
        {
            GetEditorManagerNode()?.RegisterDoor(this);
        }
    }

    public override void _ExitTree()
    {
        // A duplicate must not wipe the real Door's singleton or unregister it.
        if (isDuplicate)
        {
            return;
        }

        if (area != null)
        {
            area.BodyEntered -= OnAreaEntered;
            area.BodyExited -= OnAreaExited;
        }

        if (instance == this)
        {
            instance = null;
        }

        if (Runtime.editorMode)
        {
            GetEditorManagerNode()?.UnregisterDoor();
        }
    }

    public override void _Process(double delta)
    {
        if (isDuplicate || cacheLevelCompleted)
        {
            return;
        }

        Gorgonzola gorg = Gorgonzola.GetInstance();
        bool grounded = gorg != null && gorg.IsOnFloor();

        if (grounded != lastGrounded)
        {
            lastGrounded = grounded;
            UpdateIndicator();
        }

        if (opened && inRange && grounded && Input.IsActionJustPressed("interact"))
        {
            Enemy.KillAll?.Invoke();

            cacheLevelCompleted = true;
            SetLevelCompleted(true);
            UpdateIndicator();
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
        // Can be reached on a duplicate / freed Door (e.g. a clone touching the key).
        if (isDuplicate || !IsInstanceValid(this) || animTree == null || !IsInstanceValid(animTree))
        {
            return;
        }

        foreach (string param in animationParams)
        {
            animTree.Set($"parameters/conditions/{param}", param == activeParam);
        }
    }

    public static Door GetInstance()
    {
        return instance;
    }

    public Vector2 GetColliderPos()
    {
        return area.GlobalPosition;
    }

    private void UpdateIndicator()
    {
        if (isDuplicate)
        {
            return;
        }

        Gorgonzola gorg = Gorgonzola.GetInstance();
        if (gorg == null)
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
        }

        gorg.SetIndicatorVisibility(shouldShow);
    }

    // Writes levelCompleted to whichever manager is active, if it still exists.
    private void SetLevelCompleted(bool value)
    {
        if (Runtime.editorMode)
        {
            var egm = EditorGameManager.GetInstance();
            if (egm != null)
            {
                egm.levelCompleted = value;
            }
        }
        else
        {
            var ggm = GlobalGameManager.GetInstance();
            if (ggm != null)
            {
                ggm.levelCompleted = value;
            }
        }
    }

    private EditorGameManager GetEditorManagerNode()
    {
        return GetTree()?.CurrentScene?.GetNodeOrNull<EditorGameManager>(EditorGameManagerPath);
    }

    private void OnFlush()
    {
        QueueFree();
    }
}