using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public partial class Ui : Control
{
    public LevelEditorMain main;

    public Label toolBarLabel;

    [Export] public QuitMenu quitMenu;

    public ColorRect blockMouse;

    private bool levelLoaded => !string.IsNullOrEmpty(((Runtime)GetTree().CurrentScene).filePath);

    public override void _Ready()
    {
        main = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main");

        toolBarLabel = GetNode<Label>("ToolBar/ToolBarLabel");

        quitMenu.HideMenu();

        blockMouse = GetNode<ColorRect>("BlockMouse");
        blockMouse.MouseFilter = MouseFilterEnum.Ignore;

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;


    }

    private void OnMouseEntered()
    {
        EditorObject editor = GetTree().CurrentScene.GetNode<EditorObject>("editor/main/EditorObject");
        editor.canPlace = true;
        editor.hideCursor = false;
    }

    private void OnMouseExited()
    {
        EditorObject editor = GetTree().CurrentScene.GetNode<EditorObject>("editor/main/EditorObject");
        editor.canPlace = false;
        editor.hideCursor = true;
    }

    public override void _Process(double delta)
    {
        // Center quitMenu within this UI control
        quitMenu.Position = (Size - quitMenu.Size) / 2;

        toolBarLabel.Text = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main").levelName + (EditorObject.GetInstance().History.IsDirty ? " (Unsaved)" : "");
    }
}
