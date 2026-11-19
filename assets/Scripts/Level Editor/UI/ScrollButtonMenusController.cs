using Godot;
using System;

public partial class ScrollButtonMenusController : Control
{
    [Export] public int assetsMenuMode = 1;

    [Export] Godot.Collections.Array<Control> scrollButtonMenus;
    [Export] Godot.Collections.Array<string> scrollButtonMenuNames;

    public Button leftButton;
    public TextureRect leftSprite;
    public AnimationPlayer leftPlayer;
    public Button rightButton;
    public TextureRect rightSprite;
    public AnimationPlayer rightPlayer;
    public Label assetsLabel;

    public override void _Ready()
    {
        leftButton = GetNode<Button>("LeftButton");
        leftButton.Pressed += LeftButtonPressed;
        leftSprite = GetNode<TextureRect>("LeftSprite");
        leftPlayer = leftSprite.GetNode<AnimationPlayer>("player");
        rightButton = GetNode<Button>("RightButton");
        rightButton.Pressed += RightButtonPressed;
        rightSprite = GetNode<TextureRect>("RightSprite");
        rightPlayer = rightSprite.GetNode<AnimationPlayer>("player");
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

        leftPlayer.Play(leftButton.ButtonPressed ? "pressed" : "default");
        rightPlayer.Play(rightButton.ButtonPressed ? "pressed" : "default");
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
