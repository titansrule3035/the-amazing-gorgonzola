using Godot;
using System;

public partial class ToolBarMenu : Control
{
    public ToolBarButton parentButton;
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            bool clickedThis = GetGlobalRect().HasPoint(mb.GlobalPosition);
            bool clickedParent = parentButton.GetGlobalRect().HasPoint(mb.GlobalPosition);

            if (!clickedThis && !clickedParent)
            {
                parentButton.UpdateMenuAndButton(false);
            }
        }
    }
}
