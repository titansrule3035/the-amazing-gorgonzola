using Godot;
using System;
using System.Threading.Tasks;

public partial class Gorgonzola : BasePlayerController
{
    private static Gorgonzola instance;

    public event Action? OnKilled;

    private PackedScene carcassEffectScene = GD.Load<PackedScene>("res://Assets/Scenes/Effects/gorg_carcass.tscn");

    public bool doorMoveTriggered = false;

    private bool _hasMovedToDoor = false;

    public override void _Ready()
    {
        if (instance != null)
        {
            GD.PrintErr("More than one Gorgonzola instances exist! Deleting this one...");
            QueueFree();
            return;
        }
        instance = this;

        base._Ready();

        var spawn = SpawnGorg.GetInstance();
        if (spawn != null)
        {
            spawn.GorgSpawned += OnSpawnPointFound;
        }
        GlobalGameManager.GetInstance().OnFlush += Flush;
    }


    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    public void MoveToDoor()
    {
        Vector2 endPos = new Vector2(Door.GetInstance().GlobalPosition.X, GlobalPosition.Y);

        float moveTime = .4f;

        Tween tween = CreateTween();

        tween.TweenProperty(this, "global_position", endPos, moveTime);
    }

    protected override float GetMovementInput()
    {
        return Input.GetAxis("move_left", "move_right");
    }

    public override bool ShouldFlipSprite(float direction)
    {
        shouldFlip = (direction < 0);
        return shouldFlip;
    }

    public override void Kill()
    {
        OnKilled?.Invoke();
        BasePlayerController.KillAllClones();
        GorgCarcass effect = carcassEffectScene.Instantiate<GorgCarcass>();
        effect.flip = shouldFlip;
        GetTree().Root.AddChild(effect);
        effect.GlobalPosition = GlobalPosition;
        QueueFree();
    }

    public static Gorgonzola GetInstance()
    {
        return instance;
    }

    public override void _ExitTree()
    {
        if (instance == this)
        {
            instance = null;
        }

        base._ExitTree();

    }

    public void OnSpawnPointFound(Gorgonzola gorg)
    {
        // Unsubscribe once we got the event
        var spawn = SpawnGorg.GetInstance();
        if (spawn != null)
        {
            spawn.GorgSpawned -= OnSpawnPointFound;
        }
    }
    public void ShowVictoryMenu(bool condition) => GlobalGameManager.GetInstance().ShowVictoryMenu(condition);
}
