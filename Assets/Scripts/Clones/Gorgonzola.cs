using Godot;
using System;
using System.Threading.Tasks;

public partial class Gorgonzola : BasePlayerController
{
    // Singleton / instance
    private static Gorgonzola instance;

    // Events
    public event Action? OnKilled;
    public static event Action? OnMovementUpdated;

    // Scenes / resources
    [Export] private PackedScene carcassEffectScene;

    // State
    public bool doorMoveTriggered = false;

    // Internal state
    private bool hasMovedToDoor = false;

    public override void _Ready()
    {
        /// <summary>
        /// Initialize the singleton instance, wire spawn signals and game manager flush handlers, and call base Ready.
        /// </summary>
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

        if (useGGM)
        {
            GlobalGameManager.GetInstance().OnFlush += Flush;
        }
    }


    public override void _Process(double delta)
    {
        /// <summary>
        /// Per-frame process; if door movement has been triggered, start moving to the door.
        /// </summary>
        if (doorMoveTriggered)
        {
            doorMoveTriggered = false;
            MoveToDoor();
        }
        base._Process(delta);
    }

    /// <summary>
    /// Waits for a short delay then tweens this node's global position to the door's X position.
    /// </summary>
    public async void MoveToDoor()
    {
        await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);

        Vector2 endPos = new(Door.GetInstance().GlobalPosition.X, GlobalPosition.Y);

        float moveTime = .4f;

        Tween tween = CreateTween();

        tween.TweenProperty(this, "global_position", endPos, moveTime);
    }

    protected override float GetMovementInput()
    {
        /// <summary>
        /// Provides horizontal movement input for the main player.
        /// </summary>
        return Input.GetAxis("move_left", "move_right");
    }

    public override bool ShouldFlipSprite(float direction)
    {
        /// <summary>
        /// Updates the flip state based on direction and returns the state.
        /// </summary>
        shouldFlip = (direction < 0);
        return shouldFlip;
    }

    public override void Kill()
    {
        /// <summary>
        /// Handles main player death: notifies listeners, kills clones, spawns carcass effect and frees this node.
        /// </summary>
        OnKilled?.Invoke();
        BasePlayerController.KillAllClones();
        if (useGGM)
        {
            GlobalGameManager.GetInstance().canMove = false;
        }
        else
        {
            BasePlayerController.SetCloneMovement(false);
        }
        GorgCarcass effect = carcassEffectScene.Instantiate<GorgCarcass>();
        effect.flip = shouldFlip;
        GetTree().Root.AddChild(effect);
        effect.GlobalPosition = GlobalPosition;
        QueueFree();

        base.Kill();
    }

    public static Gorgonzola GetInstance()
    {
        /// <summary>
        /// Returns the singleton instance of Gorgonzola, or null if not present.
        /// </summary>
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
        var spawn = SpawnGorg.GetInstance();
        if (spawn != null)
        {
            spawn.GorgSpawned -= OnSpawnPointFound;
        }
    }
}
