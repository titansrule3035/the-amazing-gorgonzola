using Godot;
using Godot.Collections;
using System;

public partial class ToolBar : Control
{
    public bool MenuOpen => fileButton.showMenu || windowButton.showMenu || editorButton.showMenu;
    public FileButton fileButton;
    public WindowButton windowButton;
    public EditorButton editorButton;

    public override void _Ready()
    {
        fileButton = GetNode<FileButton>("FileButton");
        fileButton.toolBarButtonPressed += ArrangeMenuVisibility;
        fileButton.toolBarButtonHovered += HoverMenu;
        windowButton = GetNode<WindowButton>("WindowButton");
        windowButton.toolBarButtonPressed += ArrangeMenuVisibility;
        windowButton.toolBarButtonHovered += HoverMenu;
        editorButton = GetNode<EditorButton>("EditorButton");
        editorButton.toolBarButtonPressed += ArrangeMenuVisibility;
        editorButton.toolBarButtonHovered += HoverMenu;
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
        fileButton.showMenu = false;
        windowButton.showMenu = false;
        editorButton.showMenu = false;
    }

    void ArrangeMenuVisibility(ToolBarButton button)
    {
        bool wasOpen = button.showMenu;

        CloseMenus();

        button.showMenu = !wasOpen;
    }

    private void HoverMenu(ToolBarButton button)
    {
        if (!MenuOpen)
            return;

        if (button.showMenu)
            return;

        CloseMenus();
        button.showMenu = true;
    }
}
