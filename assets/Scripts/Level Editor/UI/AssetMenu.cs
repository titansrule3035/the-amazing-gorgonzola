
using Godot;
using System;

public partial class AssetMenu : Control
{
    private Control contentClip;
    private VBoxContainer content;
    private VScrollBar scrollBar;

    private BaseButton topButton;
    private BaseButton bottomButton;

    private bool syncing = false;

    public override void _Ready()
    {
        contentClip = GetNode<Control>("ContentClip");
        content = GetNode<VBoxContainer>("ContentClip/Content");
        scrollBar = GetNode<VScrollBar>("Scroll/VScrollBar");

        topButton = GetNode<BaseButton>("Scroll/TopButton");
        bottomButton = GetNode<BaseButton>("Scroll/BottomButton");

        // Clip anything outside the visible area.
        contentClip.ClipContents = true;

        // Configure scrollbar range.
        scrollBar.MinValue = 0;
        scrollBar.MaxValue = 1;

        // Connect scrollbar.
        scrollBar.ValueChanged += OnScrollChanged;

        // Connect buttons.
        topButton.Pressed += ScrollUp;
        bottomButton.Pressed += ScrollDown;

        // Update when viewport changes size.
        contentClip.Resized += RefreshScrollBar;

        // Update when children are added or removed.
        content.ChildEnteredTree += OnContentChanged;
        content.ChildExitingTree += OnContentChanged;

        CallDeferred(nameof(UpdateScrollBar));
    }

    // Custom scrollbar -> Content
    private void OnScrollChanged(double value)
    {
        if (syncing)
        {
            return;
        }

        float maxScroll = GetMaxScroll();
        double maxValue = GetScrollRange();

        double normalized = maxValue > 0 ? value / maxValue : 0;

        content.Position = new Vector2(content.Position.X, -(float)(normalized * maxScroll));
    }

    // Calculate actual content height.
    private float GetContentHeight()
    {
        float height = content.GetCombinedMinimumSize().Y;

        foreach (Node child in content.GetChildren())
        {
            if (child is Control control)
            {
                height = Mathf.Max(height, control.Position.Y + control.Size.Y);
            }
        }

        return height;
    }

    private float GetMaxScroll()
    {
        return Mathf.Max(0, GetContentHeight() - contentClip.Size.Y);
    }

    // Effective scrollbar range, accounting for Page.
    private double GetScrollRange()
    {
        return Mathf.Max(0, scrollBar.MaxValue - scrollBar.Page - scrollBar.MinValue);
    }

    // Recalculate scrollbar and content dimensions.
    private void UpdateScrollBar()
    {
        syncing = true;

        float previousMaxScroll = GetMaxScroll();
        float previousPosition = -content.Position.Y;

        float contentHeight = GetContentHeight();

        content.Size = new Vector2(contentClip.Size.X, contentHeight);

        float maxScroll = GetMaxScroll();
        double maxValue = GetScrollRange();

        scrollBar.Visible = maxScroll > 0;

        topButton.Disabled = bottomButton.Disabled = !scrollBar.Visible;

        if (maxScroll <= 0 || maxValue <= 0)
        {
            scrollBar.Value = scrollBar.MinValue;

            content.Position = new Vector2(content.Position.X, 0);
        }
        else
        {
            double normalized = previousMaxScroll > 0 ? previousPosition / previousMaxScroll : 0;

            normalized = Mathf.Clamp((float)normalized, 0, 1);

            scrollBar.Value = scrollBar.MinValue + normalized * maxValue;

            content.Position = new Vector2(content.Position.X, -(float)(normalized * maxScroll));
        }

        syncing = false;
    }

    // Scroll upward by one scrollbar step.
    private void ScrollUp()
    {
        scrollBar.Value = Mathf.Max(scrollBar.MinValue, scrollBar.Value - scrollBar.Step);
    }

    // Scroll downward by one scrollbar step.
    private void ScrollDown()
    {
        scrollBar.Value = Mathf.Min(scrollBar.MaxValue - scrollBar.Page, scrollBar.Value + scrollBar.Step);
    }

    // Call after adding or removing assets.
    public void RefreshScrollBar()
    {
        CallDeferred(nameof(UpdateScrollBar));
    }

    private void OnContentChanged(Node child)
    {
        RefreshScrollBar();
    }
}
