using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public partial class Ui : Control
{
    public Main main;

    public Label toolBarLabel;

    [Export] public QuitMenu quitMenu;

    public ColorRect blockMouse;


    private bool levelLoaded => !string.IsNullOrEmpty(main.filePath);

    public override void _Ready()
    {
        main = GetTree().CurrentScene as Main;

        toolBarLabel = GetNode<Label>("ToolBar/ToolBarLabel");

        quitMenu.HideMenu();

        blockMouse = GetNode<ColorRect>("BlockMouse");
        blockMouse.MouseFilter = MouseFilterEnum.Ignore;

        /*
        startButton.Pressed += () =>
        {
            GetTree().Paused = false;
            UpdateGameButtonStates();
        };

        restartButton = gameMenu.GetNode<Button>("RestartButton");

        restartButton.Pressed += () =>
        {
            if (!string.IsNullOrEmpty(main.filePath))
            {
                main.ImportLevel(
                    main.GetNode("level"),
                    LevelData.Decode(File.ReadAllText(main.filePath))
                );

                UpdateGameButtonStates();
            }
        };

        restartButton.Disabled = true;

        stopButton = gameMenu.GetNode<Button>("StopButton");

        stopButton.Pressed += () =>
        {
            GetTree().Paused = true;
            UpdateGameButtonStates();
        };

        stopButton.Disabled = true;
        */

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;


    }

    private void OnMouseEntered()
    {
        EditorObject editor = GetTree().CurrentScene.GetNode<EditorObject>("EditorObject");
        editor.canPlace = true;
        editor.hideCursor = false;
    }

    private void OnMouseExited()
    {
        EditorObject editor = GetTree().CurrentScene.GetNode<EditorObject>("EditorObject");
        editor.canPlace = false;
        editor.hideCursor = true;
    }

    public override void _Process(double delta)
    {
        // Center quitMenu within this UI control
        quitMenu.Position = (Size - quitMenu.Size) / 2;
    }

    public void UpdateGameButtonStates()
    {
        /*
        // Restart depends only on having a level loaded
        restartButton.Disabled = !levelLoaded;

        // Stop depends on whether the game is currently running
        stopButton.Disabled = GetTree().Paused;

        // Start depends on whether the game is currently stopped
        startButton.Disabled = !GetTree().Paused;
        */
    }
}
