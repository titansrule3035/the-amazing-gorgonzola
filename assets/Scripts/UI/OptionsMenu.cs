using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;

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

        volumeSlider = GetNode<Slider>("h_slider");
        volumeSlider.MinValue = 0;
        volumeSlider.MaxValue = 100;

        colorButtons = new();

        foreach (Button button in GetNode<Control>("color_buttons").GetChildren())
        {
            colorButtons.Add(button);
            button.Pressed += () =>
            {
                if (button.GetThemeStylebox("normal") is StyleBoxFlat flatStyleBox)
                {
                    Godot.Color buttonColor = flatStyleBox.BgColor;
                    ColorButtonPressed(buttonColor);
                }
            };
        }

        backButton.Pressed += () => backButtonPressed?.Invoke();

    }

    public override void _Process(double delta)
    {
        GlobalGameManager.GetInstance().UpdateBusVolume("Master", (float)volumeSlider.Value);

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

    void ColorButtonPressed(Godot.Color color)
    {
        Filter filter = Filter.GetInstance();

        Godot.Color filterColor = new();

        if (color.R == 1 && color.G == 1 && color.B == 1)
        {
            filterColor = new Godot.Color(0, 0, 0, 0);
        }
        else
        {
            filterColor = new(color.R * 255, color.G * 255, color.B * 255, .5f);
        }
        
        filter.SetPanelColor(filterColor);
    }
}
