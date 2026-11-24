using Godot;
using System;

public partial class FileButton : ToolBarButton
{
    public FileDialog importDialog;
    public FileDialog exportDialog;
    public FileDialog exportAsDialog;

    public Action<string>? filePicked;
    public Action<string>? fileSaved;

    LevelEditorMain main;

    public override void _Ready()
    {
        base._Ready();

        main = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main");
        GetTree().CurrentScene.GetNode<QuitMenu>("editor/main/CanvasLayer/UI/QuitMenu").cancelButton.Pressed += HideMenuAndEnableClicks;

        menuButtons[0] = menu.GetNode<Button>("OpenButton/Button");
        menuButtons[1] = menu.GetNode<Button>("SaveButton/Button");
        menuButtons[2] = menu.GetNode<Button>("SaveAsButton/Button");
        menuButtons[3] = menu.GetNode<Button>("CloseButton/Button");

        menuButtons[0].Pressed += OpenButtonPressed;
        menuButtons[1].Pressed += SaveButtonPressed;
        menuButtons[2].Pressed += SaveAsButtonPressed;
        menuButtons[3].Pressed += CloseButtonPressed;
        importDialog = GetTree().CurrentScene.GetNode<FileDialog>("editor/main/CanvasLayer/UI/ImportScreen/Dialog");

        importDialog.FileSelected += OnFileSelected;

        importDialog.VisibilityChanged += () => OnDialogVisibilityChanged(importDialog);

        exportDialog = GetTree().CurrentScene.GetNode<FileDialog>("editor/main/CanvasLayer/UI/ExportScreen/Dialog");

        exportDialog.FileSelected += OnFileSaved;

        exportDialog.VisibilityChanged += () => OnDialogVisibilityChanged(exportDialog);

        exportAsDialog = GetTree().CurrentScene.GetNode<FileDialog>("editor/main/CanvasLayer/UI/ExportAsScreen/Dialog");

        exportAsDialog.FileSelected += OnFileSaved;

        exportAsDialog.VisibilityChanged += () => OnDialogVisibilityChanged(exportAsDialog);

        foreach (var dialog in new[] { importDialog, exportDialog, exportAsDialog })
        {
            dialog.UseNativeDialog = false;
            dialog.Access = FileDialog.AccessEnum.Filesystem;
            dialog.CurrentDir = Runtime.GetLevelsDirectory();
        }

    }

    public override void _Process(double delta)
    {
        if (!main.IsInputBlocked && Input.IsActionPressed("ctrl"))
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
                GetTree().CurrentScene.GetNode<QuitMenu>("editor/main/CanvasLayer/UI/QuitMenu").SetMenuVisibility(true);
            }
        }

        if (Input.IsActionJustPressed("escape"))
        {
            foreach (var dialog in new[] { importDialog, exportDialog, exportAsDialog })
            {
                if (dialog.Visible && main.IsInputRestrictedBy(dialog))
                {
                    dialog.Hide();
                }
            }
        }
    }
    void OpenButtonPressed()
    {
        main.SetInputRestriction(importDialog, true, true);
        importDialog.PopupCenteredRatio();
    }
    void SaveButtonPressed()
    {
        main.SetInputRestriction(exportDialog, true, true);
        exportDialog.PopupCenteredRatio();
    }
    void SaveAsButtonPressed()
    {
        main.SetInputRestriction(exportAsDialog, true, true);
        exportAsDialog.PopupCenteredRatio();
    }
    async void CloseButtonPressed()
    {
        Runtime runtime = Runtime.GetInstance();
        UpdateMenuAndButton(false);
        runtime.filePath = "";
        LevelEditorMain main = GetTree().CurrentScene.GetNode<LevelEditorMain>("editor/main");
        main.levelName = "Untitled";
        EditorObject.GetInstance().history.Clear();
        runtime.ClearGroups();
        main.SetGameState(true);
        main.GetNode<EditorObject>("EditorObject").itemObject?.SetActiveItem(null);
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

    private void OnDialogVisibilityChanged(FileDialog dialog)
    {
        GD.Print(dialog.Visible);
        main.SetInputRestriction(dialog, dialog.Visible, dialog.Visible);
        dialog.GetParent().GetNode<Control>("MouseBlock").MouseFilter = dialog.Visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        GD.Print(dialog.GetParent().GetNode<Control>("MouseBlock").MouseFilter);
        if (!dialog.Visible)
        {
            UpdateMenuAndButton(false);
        }

    }
}
