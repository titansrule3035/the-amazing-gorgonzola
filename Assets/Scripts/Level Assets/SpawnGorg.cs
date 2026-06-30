using Godot;
using System;

public partial class SpawnGorg : Area2D
{
    // Singleton / resources
    private static SpawnGorg instance;

    [Export] private PackedScene gorgonzolaScene;

    // Events
    public event Action<Gorgonzola> GorgSpawned;

    // State
    private bool flip;

    private enum Direction
    {
        Left,
        Right
    }

    [Export] private Direction direction;

    /// <summary>
    /// Initializes the spawn point singleton, determines flip based on the configured direction,
    /// and ensures a Gorgonzola instance exists in the scene.
    /// </summary>
    public override async void _Ready()
    {
        if (instance != null)
        {
            GD.PrintErr("More than one SpawnGorg exists! Deleting this one...");
            QueueFree();
            return;
        }

        instance = this;

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        flip = (direction == Direction.Left);

        Gorgonzola gorg = Gorgonzola.GetInstance();
        if (gorg == null)
        {
            gorg = gorgonzolaScene.Instantiate<Gorgonzola>();

            GetTree().CurrentScene.GetNode<Node2D>("level/level_assets/clones").CallDeferred("add_child", gorg);
        }
    }

    /// <summary>
    /// Polls each frame to detect when a Gorgonzola instance becomes available, then configures it and fires GorgSpawned.
    /// </summary>
    public override void _Process(double delta)
    {
        Gorgonzola gorg = Gorgonzola.GetInstance();
        if (gorg != null)
        {
            SetupSpawn(gorg);

            GorgSpawned?.Invoke(gorg);

            QueueFree();
        }
    }

    /// <summary>
    /// Applies this spawn's position and flip to the provided Gorgonzola instance.
    /// </summary>
    private void SetupSpawn(Gorgonzola gorg)
    {
        gorg.GlobalPosition = GlobalPosition;
        gorg.sprite.FlipH = gorg.shouldFlip = flip;
    }

    /// <summary>
    /// Returns the singleton SpawnGorg instance if present.
    /// </summary>
    public static SpawnGorg GetInstance()
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
}
