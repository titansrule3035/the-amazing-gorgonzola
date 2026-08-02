using Godot;
using System;

public partial class FileButton : ToolBarButton
{
    public FileDialog importDialog;
    public FileDialog exportDialog;
    public FileDialog exportAsDialog;

    public Action<string>? filePicked;
    public Action<string>? fileSaved;

    public override void _Ready()
    {
        GetTree().CurrentScene.GetNode<QuitMenu>("CanvasLayer/UI/QuitMenu").cancelButton.Pressed += HideMenuAndEnableClicks;

        menuButtons[0] = menu.GetNode<Button>("OpenButton/Button");
        menuButtons[1] = menu.GetNode<Button>("SaveButton/Button");
        menuButtons[2] = menu.GetNode<Button>("SaveAsButton/Button");
        menuButtons[3] = menu.GetNode<Button>("CloseButton/Button");

        menuButtons[0].Pressed += OpenButtonPressed;
        menuButtons[1].Pressed += SaveButtonPressed;
        menuButtons[2].Pressed += SaveAsButtonPressed;
        menuButtons[3].Pressed += CloseButtonPressed;
        importDialog = GetTree().CurrentScene.GetNode<FileDialog>("CanvasLayer/UI/ImportDialog");

        importDialog.FileSelected += OnFileSelected;

        importDialog.VisibilityChanged += () =>
        {
            if (!importDialog.Visible)
            {
                blockMouse.MouseFilter = MouseFilterEnum.Ignore;
                UpdateMenuAndButton(false);
            }
        };

        exportDialog = GetTree().CurrentScene.GetNode<FileDialog>("CanvasLayer/UI/ExportDialog");

        exportDialog.FileSelected += OnFileSaved;

        exportDialog.VisibilityChanged += () =>
        {
            if (!exportDialog.Visible)
            {
                blockMouse.MouseFilter = MouseFilterEnum.Ignore;
                UpdateMenuAndButton(false);
            }
        };

        exportAsDialog = GetTree().CurrentScene.GetNode<FileDialog>("CanvasLayer/UI/ExportAsDialog");

        exportAsDialog.FileSelected += OnFileSaved;

        exportAsDialog.VisibilityChanged += () =>
        { 
            if (!exportDialog.Visible)
            {
                blockMouse.MouseFilter = MouseFilterEnum.Ignore;
                UpdateMenuAndButton(false);
            }
        };

        blockMouse = GetTree().CurrentScene.GetNode<ColorRect>("CanvasLayer/UI/BlockMouse");
        blockMouse.MouseFilter = MouseFilterEnum.Ignore;

        base._Ready();
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionPressed("ctrl"))
        {
            if (Input.IsActionJustPressed("o"))
            {
                OpenButtonPressed();
            }
            if (Input.IsActionJustPressed("s") && !Input.IsActionPressed("shift"))
            {
                SaveButtonPressed();
            }
            if (Input.IsActionPressed("shift"))
            {
                if (Input.IsActionJustPressed("s"))
                {
                    SaveAsButtonPressed();
                }
            }
            if (Input.IsActionJustPressed("c"))
            {
                CloseButtonPressed();
            }
            if (Input.IsActionJustPressed("q"))
            {
                ((Main)GetTree().CurrentScene).ui.quitMenu.SetMenuVisibility(true);
            }

        }
        base._Process(delta);
    }
    void OpenButtonPressed()
    {
        blockMouse.MouseFilter = MouseFilterEnum.Stop;
        importDialog.PopupCenteredRatio();
    }
    void SaveButtonPressed()
    {
        blockMouse.MouseFilter = MouseFilterEnum.Stop;
        exportDialog.PopupCenteredRatio();
    }
    void SaveAsButtonPressed()
    {
        blockMouse.MouseFilter = MouseFilterEnum.Stop;
        exportAsDialog.PopupCenteredRatio();
    }
    void CloseButtonPressed()
    {
        UpdateMenuAndButton(false);
        Main main = (GetTree().CurrentScene as Main);
        GetParent().GetNode<Label>("ToolBarLabel").Text = "Untitled (Unsaved)";
        main.filePath = "";
        main.ClearGroups();
        main.SetGameState(true);
    }
    void OnFileSelected(string path)
    {
        filePicked?.Invoke(path);
    }

    void OnFileSaved(string path)
    {
        fileSaved?.Invoke(path);
    }

    void HideMenuAndEnableClicks()
    {
        UpdateMenuAndButton(false);
    }
}
