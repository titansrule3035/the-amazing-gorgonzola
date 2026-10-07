using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using TheAmazingGorgonzola.assets.Scripts.Level_Assets;
using static SemiSolidTileData;

public partial class LevelEditorMain : Node2D
{
    public Ui ui;

    public FileButton fileButton;

    public Gorgonzola gorgonzola;

    public ScrollButtonMenusController scrollButton;

    public Door door = null;
    public Action? OnDoorRegistered;
    public Action? OnDoorUnregistered;

    public Key key = null;
    public Action? OnKeyRegistered;
    public Action? OnKeyUnregistered;

    public Action? OnGameStarted;
    public Action? OnGamePaused;

    // Map each object's "Type" (the original node Name) to the scene that should be instantiated.
    // Populate these from the Editor or load them by convention, e.g. res://objects/{type}.tscn

    public override void _Ready()
    {
        ui = GetNode<Ui>("CanvasLayer/UI");
        ui.Visible = true;

        fileButton = GetNode<FileButton>("CanvasLayer/UI/ToolBar/FileButton");

        EditorGameManager.GetInstance().canPause = false;

        KillPanel.GetInstance().OnFadeOut += OnFadeOut;

        scrollButton = GetNode<ScrollButtonMenusController>("CanvasLayer/UI/ScrollButton");

        SetGameState(true);

        GetWindow().FocusExited += GetNode<ToolBar>("CanvasLayer/UI/ToolBar").CloseMenus;

        Input.SetCustomMouseCursor(null, Input.CursorShape.Arrow, new(0, 0));

        Runtime runtime = (Runtime)GetTree().CurrentScene;

        fileButton.filePicked += runtime.FilePicked;
        runtime.OnFilePicked += FilePicked;
        fileButton.fileSaved += runtime.FileSaved;
        runtime.OnFileSaved += FileSaved;
        runtime.OnLevelImported += ImportLevel;
    }

    public override void _Process(double delta)
    {
        // No per-frame subscriptions here. Gorgonzola kill handler is wired
        // by EditorGameManager.RegisterGorg to avoid repeated subscriptions.
    }

    void FilePicked(string filePath)
    {
        ui.toolBarLabel.Text = Path.GetFileNameWithoutExtension(filePath);
    }

    public void FileSaved(string filePath)
    {
        if (Path.GetFileName(filePath) != ".taglevel")
        {
            ui.toolBarLabel.Text = Path.GetFileNameWithoutExtension(filePath);
        }
    }

    public void ImportLevel()
    {
        SetGameState(true);

        ui.UpdateGameButtonStates();
    }

    public void ResetGame()
    {
        Input.ActionPress("reset");
    }

    public void OnGorgKilled()
    {
        KillPanel.GetInstance().FadeOut();
    }

    public async void OnFadeOut()
    {
        Runtime runtime = (Runtime)GetTree().CurrentScene;
        await runtime.ImportLevel(GetNode("level"), LevelData.Decode(File.ReadAllText(Path.Combine(OS.GetUserDataDir(), "tmp/.taglevel"))));

        GetTree().CurrentScene.GetNode<Camera2D>("editor/main/Camera2D").GlobalPosition = new(-224, -400);

        SetGameState(true);

        KillPanel.GetInstance().FadeIn();
    }

    public void SetGameState(bool paused)
    {
        GetTree().Paused = paused;

        Button playButton = GetNode<Button>("CanvasLayer/UI/GameControlsMenu/PlayButtonControl/CenterContainer/PlayButton");

        if (paused)
        {
            playButton.Text = "Play";
            OnGamePaused?.Invoke();
        }
        else
        {
            playButton.Text = "Stop";
            OnGameStarted?.Invoke();
        }
    }

    public void ToggleGameState()
    {
        SetGameState(!GetTree().Paused);
    }

    public void RegisterDoor(Door door)
    {
        this.door = door;
        OnDoorRegistered?.Invoke();
        GD.Print("Door registered.");
    }

    public void UnregisterDoor()
    {
        door = null;
        OnDoorUnregistered?.Invoke();
    }
    public void RegisterKey(Key key)
    {
        this.key = key;
        OnKeyRegistered?.Invoke();
    }

    public void UnregisterKey()
    {
        key = null;
        OnKeyUnregistered?.Invoke();
    }
}