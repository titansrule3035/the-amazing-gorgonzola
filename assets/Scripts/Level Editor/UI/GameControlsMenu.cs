using Godot;
using System;
using System.IO;

public partial class GameControlsMenu : Control
{
    Button PlayButton;
    Button HelpButton;
    Button UndoButton;
    Button RedoButton;
    Button ResetButton;
    Button SelectButton;
    Button EraseButton;

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

    private void PlayButtonPressed()
    {
        Main main = GetTree().CurrentScene as Main;

        main.ToggleGameState();

        if (!GetTree().Paused)
        {
            if (!DirAccess.DirExistsAbsolute("user://tmp"))
            {
                DirAccess.MakeDirAbsolute("user://tmp");
            }

            ((Main) GetTree().CurrentScene).FileSaved(Path.Combine(OS.GetUserDataDir(), "tmp", ".taglevel"));
        }
        else
        {
            ((Main) GetTree().CurrentScene).ImportLevel(GetTree().CurrentScene.GetNode("level"), LevelData.Decode(File.ReadAllText(Path.Combine(OS.GetUserDataDir(), "tmp/.taglevel"))));
        }
    }

    private void HelpButtonPressed()
    {
        throw new NotImplementedException();
    }

    private void UndoButtonPressed()
    {
        throw new NotImplementedException();
    }

    private void RedoButtonPressed()
    {
        throw new NotImplementedException();
    }

    private void ResetButtonPressed()
    {
        throw new NotImplementedException();
    }

    private void SelectButtonPressed()
    {
        throw new NotImplementedException();
    }

    private void EraseButtonPressed()
    {
        throw new NotImplementedException();
    }
}
