using Godot;
using System;
using System.IO;
using FileAccess = Godot.FileAccess;

public partial class GameControlsMenu : Control
{
    Button PlayButton;
    Button HelpButton;
    Button UndoButton;
    Button RedoButton;
    Button ResetButton;
    Button SelectButton;
    Button EraseButton;

    Runtime runtime;
    LevelEditorMain main;
    EditorObject editor;

    public override void _Ready()
    {
        PlayButton = GetNode<Button>("PlayButtonControl/CenterContainer/PlayButton");
        PlayButton.Pressed += PlayButtonPressed;
        HelpButton = GetNode<Button>("EditorToolsControl/CenterContainer/VBoxContainer/HBoxContainer/HelpButton");
        HelpButton.Pressed += HelpButtonPressed;
        UndoButton = GetNode<Button>("EditorToolsControl/CenterContainer/VBoxContainer/HBoxContainer/UndoButton");
        UndoButton.Pressed += UndoButtonPressed;
        RedoButton = GetNode<Button>("EditorToolsControl/CenterContainer/VBoxContainer/HBoxContainer/RedoButton");
        RedoButton.Pressed += RedoButtonPressed;
        ResetButton = GetNode<Button>("EditorToolsControl/CenterContainer/VBoxContainer/HBoxContainer2/ResetButton");
        ResetButton.Pressed += ResetButtonPressed;
        SelectButton = GetNode<Button>("EditorToolsControl/CenterContainer/VBoxContainer/HBoxContainer2/SelectButton");
        SelectButton.Pressed += SelectButtonPressed;
        EraseButton = GetNode<Button>("EditorToolsControl/CenterContainer/VBoxContainer/HBoxContainer2/EraseButton");
        EraseButton.Pressed += EraseButtonPressed;
    }

    public override void _Process(double delta)
    {
        runtime = GetTree().CurrentScene as Runtime;
        main = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main");
        editor = main.GetNode<EditorObject>("EditorObject");

        if (!DebugConsole.focusedOnConsole && Input.IsActionJustPressed("editor_play"))
        {
            PlayButtonPressed();
        }

        PlayButton.Text = GetTree().Paused ? "Play" : "Stop";

        UndoButton.Disabled = !editor.history.canUndo;
        RedoButton.Disabled = !editor.history.canRedo;
    }
    private async void PlayButtonPressed()
    {
        LevelEditorMain main = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main");
        Runtime runtime = GetTree().CurrentScene as Runtime;

        // save file before game begins if paused
        if (GetTree().Paused)
        {
            if (!DirAccess.DirExistsAbsolute("user://tmp"))
            {
                DirAccess.MakeDirAbsolute("user://tmp");
            }

            runtime.FileSaved(Path.Combine(OS.GetUserDataDir(), "tmp", ".taglevel"));
        }

        main.ToggleGameState();

        if (!GetTree().Paused)
        {
            if (!DirAccess.DirExistsAbsolute("user://tmp"))
            {
                DirAccess.MakeDirAbsolute("user://tmp");
            }

            EditorGameManager.GetInstance().canMove = true;

            main.GetNode<CameraDrag>("Camera2D").StorePos();
        }
        else
        {
            string filePath = Path.Combine(OS.GetUserDataDir(), "tmp", ".taglevel");

            if (!File.Exists(filePath))
            {
                using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
            }

            await ((Runtime)GetTree().CurrentScene).ImportLevel(main.GetNode("level"), LevelData.Decode(File.ReadAllText(filePath)));

            EditorGameManager.GetInstance().canMove = false;

            main.GetNode<CameraDrag>("Camera2D").RestorePos();
        }
    }

    private void HelpButtonPressed()
    {
        DebugManager.Log($"{new NotImplementedException()}", "Editor Help Button");
    }

    private void UndoButtonPressed()
    {
        editor.Undo();
    }

    private void RedoButtonPressed()
    {
        editor.Redo();
    }

    private void ResetButtonPressed()
    {
        DebugManager.Log(new NotImplementedException(), "Editor Reset Button");
    }

    private void SelectButtonPressed()
    {
        editor.SetCursorMode(EditorObject.CursorMode.Select);
    }

    private void EraseButtonPressed()
    {
        editor.SetCursorMode(EditorObject.CursorMode.Eraser);
    }
}
