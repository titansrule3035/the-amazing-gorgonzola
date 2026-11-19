using Godot;
using System;
using System.IO;
using FileAccess = Godot.FileAccess;
using CursorMode = EditorObject.CursorMode;


public partial class GameControlsMenu : Control
{
    Button PlayButton;
    [Export] TextureButton HelpButton;
    [Export] TextureButton UndoButton;
    [Export] TextureButton RedoButton;
    [Export] TextureButton MuteButton;
    [Export] TextureButton BrushButton;
    [Export] TextureButton EraseButton;

    CursorMode lastCursorMode;
    public static bool helpMenuActive = false;
    [Export] public Control helpMenu;

    Runtime runtime;
    LevelEditorMain main;
    EditorObject editor;

    public override void _Ready()
    {
        PlayButton = GetNode<Button>("PlayButtonControl/CenterContainer/PlayButton");
        PlayButton.Pressed += PlayButtonPressed;
        HelpButton.Pressed += HelpButtonPressed;
        UndoButton.Pressed += UndoButtonPressed;
        RedoButton.Pressed += RedoButtonPressed;
        MuteButton.Pressed += MuteButtonPressed;
        BrushButton.Pressed += BrushButtonPressed;
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

        HelpButton.Disabled = BrushButton.Disabled = EraseButton.Disabled = !GetTree().Paused;

        if (GetTree().Paused)
        {
            UndoButton.Disabled = !editor.history.canUndo;
            RedoButton.Disabled = !editor.history.canRedo;
        }
        else
        {
            UndoButton.Disabled = RedoButton.Disabled = true;
        }

        BrushButton.ButtonPressed = (editor.currentCursorMode == CursorMode.Place);
        EraseButton.ButtonPressed = (editor.currentCursorMode == CursorMode.Eraser);

        helpMenu.Visible = helpMenuActive;

        if (!helpMenuActive)
        {
            HelpButton.ButtonPressed = false;
        }

        if (Input.IsActionJustPressed("mb_left") && helpMenuActive)
        {
            editor.currentCursorMode = lastCursorMode;
            helpMenuActive = false;
        }
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

            Runtime.GetInstance().ImportLevel(main.GetNode("level"), LevelData.Decode(File.ReadAllText(filePath)));

            EditorGameManager.GetInstance().canMove = false;

            main.GetNode<CameraDrag>("Camera2D").RestorePos();
        }
    }

    private void HelpButtonPressed()
    {
        if (!helpMenuActive)
        {
            lastCursorMode = editor.currentCursorMode;
            editor.currentCursorMode = CursorMode.Mouse;
            helpMenuActive = true;
        }
    }

    private void UndoButtonPressed()
    {
        editor.Undo();
    }

    private void RedoButtonPressed()
    {
        editor.Redo();
    }

    private void MuteButtonPressed()
    {
        DebugManager.Log(new NotImplementedException(), "Editor Mute Button");
    }

    private void BrushButtonPressed()
    {
        if (editor.currentCursorMode == EditorObject.CursorMode.Place)
        {
            editor.SetCursorMode(EditorObject.CursorMode.Mouse);
        }
        else
        {
            editor.SetCursorMode(EditorObject.CursorMode.Place);
        }
    }

    private void EraseButtonPressed()
    {
        if (editor.currentCursorMode == EditorObject.CursorMode.Eraser)
        {
            editor.SetCursorMode(EditorObject.CursorMode.Mouse);
        }
        else
        {
            editor.SetCursorMode(EditorObject.CursorMode.Eraser);
        }
    }
}
