using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using CursorMode = EditorObject.CursorMode;

public partial class LevelEditorMain : Node2D
{
    public Ui ui;

    public FileButton fileButton;

    public ScrollButtonMenusController scrollButton;

    public Action? OnGameStarted;
    public Action? OnGamePaused;

    string lastWindowTitle = "";
    public string levelName = "Untitled";

    public EditorObject editorObject;

    public CursorMode storedMode;

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

        Runtime runtime = (Runtime)GetTree().CurrentScene;

        fileButton.filePicked += runtime.FilePicked;
        runtime.OnFilePicked += FilePicked;
        fileButton.fileSaved += runtime.FileSaved;
        runtime.OnFileSaved += FileSaved;
        runtime.OnLevelImported += ImportLevel;

        OnGameStarted += OnGameStart;

        OnGamePaused += OnGameStop;

        editorObject = EditorObject.GetInstance();
    }

    public override void _Process(double delta)
    {
        ProcessWindowTitle();
    }

    private void ProcessWindowTitle()
    {
        bool dirty = editorObject.history.IsDirty;
        string title = levelName;
        string trail;
        GetTree().AutoAcceptQuit = !dirty;

        if (string.IsNullOrWhiteSpace(levelName))
        {
            trail = (dirty ? "(Unsaved) - " : "") + "TAG Level Editor";
        }
        else
        {
            trail = (dirty ? "* (Unsaved)" : "") + " - TAG Level Editor";
        }

        title += trail;

        if (title != lastWindowTitle)
        {
            lastWindowTitle = title;
            GetWindow().Title = lastWindowTitle;
        }
    }
    void OnGameStart()
    {
        storedMode = editorObject.currentCursorMode;
        editorObject.SwitchMode(CursorMode.Mouse);
    }

    void OnGameStop()
    {
        editorObject.SwitchMode(storedMode);
    }

    void FilePicked(string filePath)
    {
        levelName = Path.GetFileNameWithoutExtension(filePath);
        editorObject.history.Clear();
    }

    public void FileSaved(string filePath)
    {
        if (Path.GetFileName(filePath) != ".taglevel")
        {
            levelName = Path.GetFileNameWithoutExtension(filePath);
            editorObject.history.MarkSaved();
        }
    }

    public void ImportLevel()
    {
        SetGameState(true);
    }

    public void ResetGame()
    {
        Input.ActionPress("reset");
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

    public override void _ExitTree()
    {
        Runtime runtime = GetTree().CurrentScene as Runtime;
        KillPanel.GetInstance()?.OnFadeOut -= OnFadeOut;
        fileButton.filePicked -= runtime.FilePicked;
        runtime.OnFilePicked -= FilePicked;
        fileButton.fileSaved -= runtime.FileSaved;
        runtime.OnFileSaved -= FileSaved;
        runtime.OnLevelImported -= ImportLevel;
    }
}