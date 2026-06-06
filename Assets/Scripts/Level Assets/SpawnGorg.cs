using Godot;
using System;

public partial class SpawnGorg : Area2D
{
    private static SpawnGorg instance;

    [Export] private PackedScene gorgonzolaScene;

    // === SIGNALS ===
    public event Action<Gorgonzola> GorgSpawned;

    private bool flip;

    private enum Direction
    {
        Left,
        Right
    }

    [Export] private Direction direction;

    public override async void _Ready()
    {
        if (instance != null)
        {
            GD.PrintErr("More than one SpawnGorg exists! Deleting this one...");
            QueueFree();
            return;
        }

        instance = this;

        // Wait one frame so Level + GameManager are fully ready
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        flip = (direction == Direction.Left);

        // If Gorgonzola is not in the scene yet, spawn one
        Gorgonzola gorg = Gorgonzola.GetInstance();
        if (gorg == null)
        {
            gorg = gorgonzolaScene.Instantiate<Gorgonzola>();

            Node levelRoot = GetTree().CurrentScene;
            levelRoot.CallDeferred("add_child", gorg);
        }
    }

    public override void _Process(double delta)
    {
        Gorgonzola gorg = Gorgonzola.GetInstance();
        if (gorg != null)
        {
            SetupSpawn(gorg);

            // Fire signal once
            GorgSpawned?.Invoke(gorg);

            QueueFree(); // Spawn point is single-use
        }
    }

    private void SetupSpawn(Gorgonzola gorg)
    {
        gorg.GlobalPosition = GlobalPosition;
        gorg.sprite.FlipH = gorg.shouldFlip = flip;
    }

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
