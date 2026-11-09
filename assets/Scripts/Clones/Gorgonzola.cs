using Godot;
using System;
using System.Threading.Tasks;

public partial class Gorgonzola : BasePlayerController
{
    // Singleton / instance
    private static Gorgonzola instance;

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
            DebugManager.Log("Only one Gorgonzola allowed per scene, deleting this one...", "GorgonzolaScript");
            QueueFree();
        }
        instance = this;
        base._Ready();

        var spawn = SpawnGorg.GetInstance();
        if (spawn != null)
        {
            spawn.GorgSpawned += OnSpawnPointFound;
        }

        Gorgonzola gorgonzola = this;
        GlobalGameManager.GetInstance()?.RegisterGorg(gorgonzola);
        EditorGameManager.GetInstance()?.RegisterGorg(gorgonzola);
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

        bool? canMove;
        if (((Runtime)GetTree().CurrentScene).editorMode)
        {
            canMove = EditorGameManager.GetInstance()?.canMove;
        }
        else
        {
            canMove = GlobalGameManager.GetInstance()?.canMove;
        }

        if (Input.IsActionJustPressed("reset") && canMove == true)
        {
            Kill();
        }

        base._Process(delta);
    }

    /// <summary>
    /// Waits for a short delay then tweens this node's global position to the door's X position.
    /// </summary>
    public async void MoveToDoor()
    {
        await ToSignal(GetTree().CreateTimer(3.0f, false), SceneTreeTimer.SignalName.Timeout);

        Vector2 endPos = new(Door.GetInstance().GlobalPosition.X, GlobalPosition.Y);

        float moveTime = .4f;

        Tween tween = CreateTween();

        tween.TweenProperty(this, "global_position", endPos, moveTime);

        await ToSignal(GetTree().CreateTimer(2.0f, false), SceneTreeTimer.SignalName.Timeout);

        GlobalGameManager.GetInstance()?.ShowVictoryMenu(true);
    }

    public override void Kill()
    {
        /// <summary>
        /// Handles main player death: notifies listeners, kills clones, spawns carcass effect and frees this node.
        /// </summary>
        if (!killed)
        {
            BasePlayerController.KillAllClones();

            KillPanel.GetInstance().FadeOut();

            GlobalGameManager? ggm = GlobalGameManager.GetInstance();

            if (ggm != null)
            {
                GlobalGameManager.GetInstance().canMove = false;
            }
            else
            {
                EditorGameManager.GetInstance().canMove = false;
            }

            base.Kill();
        }
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
        GlobalGameManager? ggm = GlobalGameManager.GetInstance();

        if (ggm != null)
        {
            ggm.UnregisterGorg();
        }
        else
        {
            EditorGameManager.GetInstance()?.UnregisterGorg();
        }

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
