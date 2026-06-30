using Godot;
using System;
using System.Collections.Generic;

public partial class EditorGameManager : Node2D
{
    private static EditorGameManager instance;

    [Export] public int activeLevelIndex = 0;
    [Export] public Godot.Collections.Array<PackedScene> levelScenes = new();

    private Node2D activeLevel;
    public LocalGameManager localGM;
    private readonly List<PackedScene> levels = new();

    private readonly HashSet<object> pauseLocks = new();
    public bool pauseLocked => pauseLocks.Count == 0;

    public event Action OnFirstFrame;
    public event Action OnLevelLoaded;
    public event Action? OnGorgFound;
    public event Action? OnGorgUnregistered;

    public Gorgonzola gorgonzola;
    public bool levelCompleted = false;
    public bool gamePaused = false;
    public bool canPause = true;
    public bool canMove = true;

    public int completedWorlds = 0;
    public int deaths = 0;
    public int clonesKilled = 0;
    public List<string> collectibles = new();

    public Main main;

    public override async void _Ready()
    {
        if (instance != null)
        {
            GD.Print("More than one EditorGameManager exists! Deleting this one...");
            QueueFree();
            return;
        }

        instance = this;

        await WaitForGameLoaded();

    }

    public override void _Process(double delta)
    {
        main = GetTree().CurrentScene as Main;

        if (main.filePath != string.Empty)
        {
            if (!levelCompleted)
            {
                if (Input.IsActionJustPressed("reset") && Gorgonzola.GetInstance() != null && canMove)
                {
                    Gorgonzola.GetInstance().CallDeferred("Kill");
                }
            }
            base._Process(delta);
        }
    }

    public override void _ExitTree()
    {
        if (instance == this)
            instance = null;

        base._ExitTree();
    }

    public static EditorGameManager GetInstance()
    {
        return instance;
    }

    private async System.Threading.Tasks.Task WaitForGameLoaded()
    {
        while (Gorgonzola.GetInstance() == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        gorgonzola = Gorgonzola.GetInstance();
        OnFirstFrame?.Invoke();
    }

    // Helper to strip trailing numbers from object names
    private static string StripTrailingNumber(string name)
    {
        int i = name.Length;
        while (i > 0 && char.IsDigit(name[i - 1]))
            i--;
        return name[..i];
    }

    // Game control
    // Set audio volume
    public void UpdateBusVolume(string busName, float linearVolume)
    {
        int busIndex = AudioServer.GetBusIndex(busName);

        float dbVolume = Mathf.LinearToDb(linearVolume);

        AudioServer.SetBusVolumeDb(busIndex, dbVolume);
    }

    public void RegisterGorg(Gorgonzola gorgonzola)
    {
        this.gorgonzola = gorgonzola;
        main = GetTree().CurrentScene as Main;
        gorgonzola.OnKilled += main.OnGorgKilled;
        OnGorgFound?.Invoke();
    }

    public void UnregisterGorg()
    {
        gorgonzola.OnKilled -= main.OnGorgKilled;
        gorgonzola = null;
        OnGorgUnregistered?.Invoke();
    }
}