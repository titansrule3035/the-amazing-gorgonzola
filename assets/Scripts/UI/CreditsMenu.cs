using System;
using System.Collections.Generic;
using System.Text;
using Godot;

public partial class CreditsMenu : Control
{
    public TextureButton backButton;

    public Action backButtonPressed { get; set; }

    public override void _Ready()
    {
        backButton.Pressed += () => backButtonPressed?.Invoke();
    }

    public void HideMenu()
    {
        if (backButton == null)
        {
            backButton = GetNode<TextureButton>("back");
        }

        backButton.Disabled = true;
        Visible = false;
    }

    public void ShowMenu()
    {
        if (backButton == null)
        {
            backButton = GetNode<TextureButton>("back");
        }

        backButton.Disabled = false;
        Visible = true;
    }
}