using Godot;
using Godot.Collections;
using System;

public partial class ToolBar : Control
{
    public FileButton fileButton;
    public WindowButton windowButton;
    public EditorButton editorButton;

    public override void _Ready()
    {
        fileButton = GetNode<FileButton>("FileButton");
        fileButton.toolBarButtonPressed += ArrangeMenuVisibility;
        windowButton = GetNode<WindowButton>("WindowButton");
        windowButton.toolBarButtonPressed += ArrangeMenuVisibility;
        editorButton = GetNode<EditorButton>("EditorButton");
        editorButton.toolBarButtonPressed += ArrangeMenuVisibility;
    }

    public override void _Process(double delta)
    {
        if (fileButton.showMenu || windowButton.showMenu || editorButton.showMenu)
        {
            if (Input.IsActionJustPressed("escape"))
            {
                CloseMenus();
            }
        }

        if (Input.IsActionPressed("alt"))
        {
            if (Input.IsActionJustPressed("f"))
            {
                fileButton.ToolBarButtonPressed();
            }
            if (Input.IsActionJustPressed("w"))
            {
                windowButton.ToolBarButtonPressed();
            }
            if (Input.IsActionJustPressed("e"))
            {
                editorButton.ToolBarButtonPressed();
            }
        }
    }

    public void CloseMenus()
    {
        fileButton.UpdateMenuAndButton(false);
        windowButton.UpdateMenuAndButton(false);
        editorButton.UpdateMenuAndButton(false);
    }

    void ArrangeMenuVisibility(ToolBarButton button)
    {

        bool wasOpen = button.showMenu;

        CloseMenus();

        button.showMenu = !wasOpen;
    }
}
