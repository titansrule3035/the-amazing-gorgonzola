using Godot;
using System;
using System.Collections.Generic;

public partial class OptionsMenu : Control
{
    // public return signal for UI
    public Action backButtonPressed { get; set; }

    // public node refs
    public TextureButton backButton;

    // private node refs
    private Slider volumeSlider;
    private List<Button> colorButtons;

    public override void _Ready()
    {
        backButton = GetNode<TextureButton>("center_container/v_box_container/back");

        volumeSlider = GetNode<Slider>("nine_patch_rect/h_slider");
        volumeSlider.MinValue = 0;
        volumeSlider.MaxValue = 100;

        colorButtons = new();

        foreach (Button button in GetNode<Control>("color_buttons").GetChildren())
        {
            colorButtons.Add(button);
        }

        backButton.Pressed += () => backButtonPressed?.Invoke();

    }

    public override void _Process(double delta)
    {
        GlobalGameManager.GetInstance().UpdateBusVolume("Master", (float) volumeSlider.Value);

        base._Process(delta);
    }

    public void HideMenu()
    {
        if (backButton == null)
        {
            backButton = GetNode<TextureButton>("center_container/v_box_container/back");
        }

        backButton.Disabled = true;
        Visible = false;
    }

    public void ShowMenu()
    {
        if (backButton == null)
            backButton = GetNode<TextureButton>("center_container/v_box_container/back");

        backButton.Disabled = false;
        Visible = true;
    }
}
