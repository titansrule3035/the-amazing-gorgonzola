using Godot;
using System;

public partial class ScrollButtonMenusController : Control
{
    [Export] public int assetsMenuMode = 1;

    [Export] Godot.Collections.Array<Control> scrollButtonMenus;
    [Export] Godot.Collections.Array<string> scrollButtonMenuNames;

    public Button leftButton;
    public Button rightButton;
    public Label assetsLabel;

    public override void _Ready()
    {
        leftButton = GetNode<Button>("LeftButton");
        leftButton.Pressed += LeftButtonPressed;
        rightButton = GetNode<Button>("RightButton");
        rightButton.Pressed += RightButtonPressed;
        assetsLabel = GetNode<Label>("TitleLabel");
    }

    public override void _Process(double delta)
    {
        if (assetsMenuMode < 1)
        {
            assetsMenuMode = scrollButtonMenus.Count;
        }
        if (assetsMenuMode > scrollButtonMenus.Count)
        {
            assetsMenuMode = 1;
        }

        // update menus visibility
        for (int i = 0; i < scrollButtonMenus.Count; i++)
        {
            if (i != assetsMenuMode - 1)
            {
                scrollButtonMenus[i].Visible = false;
            }
            else
            {
                scrollButtonMenus[i].Visible = true;
                assetsLabel.Text = scrollButtonMenuNames[i];
            }
        }

    }
    void LeftButtonPressed()
    {
        assetsMenuMode--;
    }

    void RightButtonPressed()
    {
        assetsMenuMode++;
    }
}
