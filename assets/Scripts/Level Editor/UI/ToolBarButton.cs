using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Godot.WebSocketPeer;
public partial class ToolBarButton : Button
{
    public Action<ToolBarButton>? toolBarButtonPressed;
    public Action<ToolBarButton>? toolBarButtonHovered;

    [Export] public bool showMenu = false;
    [Export] public ToolBarMenu menu;
    [Export] public Godot.Collections.Array<Button> menuButtons;
    Godot.Color fill;
    public ColorRect blockMouse;

    public override void _Ready()
    {
        Pressed += ToolBarButtonPressed;
        MouseEntered += ToolBarButtonHovered;

        blockMouse = GetTree().CurrentScene.GetNode<ColorRect>("CanvasLayer/UI/BlockMouse");

        menu.parentButton = this;

        menu.Visible = false;
        showMenu = false;
    }

    public override void _Process(double delta)
    {
        menu.Visible = showMenu;
        if (showMenu)
        {
            fill = Colors.LightGray;
        }
        else
        {
            fill = Colors.White;
        }
        GetNode<ColorRect>("ButtonForeground").Color = fill;
    }

    public void UpdateMenuAndButton(bool state)
    {
        showMenu = state;
    }

    public void ToolBarButtonPressed()
    {
        toolBarButtonPressed.Invoke(this);
    }

    private void ToolBarButtonHovered()
    {
        toolBarButtonHovered.Invoke(this);
    }
}