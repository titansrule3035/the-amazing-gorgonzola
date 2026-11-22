
using Godot;
using System;

public partial class AssetMenu : Control
{
    private Control contentClip;
    private VBoxContainer content;
    private VScrollBar scrollBar;

    private bool syncing = false;

    public override void _Ready()
    {
        contentClip = GetNode<Control>("ContentClip");
        content = GetNode<VBoxContainer>("ContentClip/Content");
        scrollBar = GetNode<VScrollBar>("VScrollBar");

        // Clip anything outside the visible area.
        contentClip.ClipContents = true;

        // Connect scrollbar.
        scrollBar.ValueChanged += OnScrollChanged;

        // Update when the viewport changes size.
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

        content.Position = new Vector2(content.Position.X, -(float)value * maxScroll);
    }

    // Calculate the actual content height.
    private float GetContentHeight()
    {
        float height = content.GetCombinedMinimumSize().Y;

        // Account for children with manually assigned heights.
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

    // Recalculate scrollbar and content dimensions.
    private void UpdateScrollBar()
    {
        syncing = true;

        float previousMaxScroll = GetMaxScroll();
        float previousPosition = -content.Position.Y;

        // Resize content to fit all children.
        float contentHeight = GetContentHeight();

        content.Size = new Vector2(contentClip.Size.X, contentHeight);

        float maxScroll = GetMaxScroll();

        scrollBar.Visible = maxScroll > 0;

        if (maxScroll <= 0)
        {
            scrollBar.Value = 0;

            content.Position = new Vector2(content.Position.X, 0);
        }
        else
        {
            // Preserve the previous normalized scroll position.
            double normalizedPosition = previousMaxScroll > 0 ? previousPosition / previousMaxScroll : scrollBar.Value;

            scrollBar.Value = Mathf.Clamp((float)normalizedPosition, 0, 1);

            content.Position = new Vector2(content.Position.X, -(float)scrollBar.Value * maxScroll);
        }

        syncing = false;
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
