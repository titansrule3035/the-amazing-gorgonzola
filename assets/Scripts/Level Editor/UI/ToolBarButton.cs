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
    [Export] public bool showMenu = false;
    [Export] public Control menu;
    [Export] public Godot.Collections.Array<Button> menuButtons;
    Godot.Color fill;

    public override void _Ready()
    {
        Pressed += ToolBarButtonPressed;

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
}