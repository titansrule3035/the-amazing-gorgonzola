using Godot;
using System;
using System.Collections.Generic;
using CursorMode = EditorObject.CursorMode;

/// <summary>
/// Base class for every cursor mode. EditorObject calls these each frame:
///   UpdateCursor()  - always (position / visuals follow the mouse)
///   Update(delta)   - only while Editor.EditorActive
///   Draw(canvas)    - from the overlay, in WORLD coordinates (call Editor.RedrawOverlay() to refresh)
/// </summary>
public abstract class EditorTool
{
    protected readonly EditorObject editor;

    protected EditorTool(EditorObject editor) => this.editor = editor;

    public abstract CursorMode mode { get; }

    /// <summary>Whether the cursor sprite should be shown in this mode.</summary>
    public virtual bool cursorVisible => false;

    /// <summary>Mid-drag / mid-stroke. Blocks the context menu.</summary>
    public virtual bool isBusy => false;

    /// <summary>Whether holding Ctrl may temporarily switch to the plain mouse.</summary>
    public virtual bool allowCtrlMouseOverride => true;

    public virtual void Enter()
    {

    }
    public virtual void Exit()
    {

    }
    public virtual void UpdateCursor()
    {

    }
    public virtual void Update(double delta)
    {

    }
    public virtual void Draw(CanvasItem canvas)
    {

    }

    /// <summary>The editor stopped being active (unpaused). End any drag or stroke cleanly.</summary>
    public virtual void OnEditorDeactivated() => Exit();
}

// ----------------------------------------------------------------------
// Mouse: plain pointer, cursor sprite hidden
// ----------------------------------------------------------------------
public class MouseTool : EditorTool
{
    public MouseTool(EditorObject editor) : base(editor)
    {

    }

    public override CursorMode mode => CursorMode.Mouse;

    public override void Enter()
    {
        editor.hideCursor = true;
    }

    public override void UpdateCursor() => editor.GlobalPosition = editor.mouseGlobal;
}

// ----------------------------------------------------------------------
// Copy / DragCamera: placeholders
// ----------------------------------------------------------------------
public class CopyTool : EditorTool
{
    public CopyTool(EditorObject editor) : base(editor) { }

    public override CursorMode mode => CursorMode.Copy;

    public override void Enter()
    {
        editor.hideCursor = true;
    }
}

public class DragCameraTool : EditorTool
{
    public DragCameraTool(EditorObject editor) : base(editor) { }

    public override CursorMode mode => CursorMode.DragCamera;

    public override void Enter()
    {
        editor.hideCursor = true;
    }
}

// ----------------------------------------------------------------------
// Place: snaps to the grid, paints tiles / spawns objects while LMB is held.
// One mouse stroke = one undo step.
// ----------------------------------------------------------------------
public class PlaceTool : EditorTool
{
    static readonly Vector2I noCell = new(int.MinValue, int.MinValue);

    Vector2I lastPlacedCell = noCell;
    bool stroke;

    public PlaceTool(EditorObject editor) : base(editor) { }

    public override CursorMode mode => CursorMode.Place;
    public override bool cursorVisible => editor.hasPlaceableItem;
    public override bool isBusy => stroke;

    public override void Enter()
    {
        lastPlacedCell = noCell;
        editor.RefreshCursorItem();
        editor.hideCursor = false;
    }

    public override void Exit() => EndStroke();

    public override void UpdateCursor()
    {
        if (editor.itemObject != null && editor.itemObject.disabled)
        {
            editor.ClearEditorItem();
        }

        // Origin sits at the BOTTOM of the hovered tile; the sprite is lifted by half
        // its height so non-tile objects stand on the bottom edge.
        float offset = editor.cursorSprite.Texture != null ? editor.cursorSprite.GetRect().Size.Y / 2f : 0f;

        editor.GlobalPosition = editor.snappedWorld + editor.halfTileDown;
        editor.cursorSprite.Position = new Vector2(0, -offset);
    }

    public override void Update(double delta)
    {
        if (!editor.canInteract || !editor.primaryHeld)
        {
            EndStroke();
            lastPlacedCell = noCell;
            return;
        }

        if (!editor.hasPlaceableItem)
        {
            return;
        }
        if (editor.cell == lastPlacedCell)
        {
            return;
        }
        if (editor.IsHoveredCellBlocked())
        {
            return;
        }

        if (!stroke)
        {
            editor.history.BeginGroup();
            stroke = true;
        }

        lastPlacedCell = editor.cell;
        editor.PlaceCurrentItem();
    }

    void EndStroke()
    {
        if (!stroke) return;

        editor.history.EndGroup();
        stroke = false;
    }
}

// ----------------------------------------------------------------------
// Eraser: follows the raw mouse, erases tile + placed objects under it.
// One mouse stroke = one undo step.
// ----------------------------------------------------------------------
public class EraserTool : EditorTool
{
    bool stroke;

    public EraserTool(EditorObject editor) : base(editor) { }

    public override CursorMode mode => CursorMode.Eraser;
    public override bool cursorVisible => true;
    public override bool isBusy => stroke;

    public override void Enter()
    {
        editor.SetCursorTexture(null);
        editor.cursorSprite.Material = null;
        editor.cursorSprite.Modulate = Colors.White;
        editor.cursorSprite.Position = Vector2.Zero;
        editor.hideCursor = true;
    }

    public override void Exit() => EndStroke();

    public override void UpdateCursor() => editor.GlobalPosition = editor.mouseGlobal;

    public override void Update(double delta)
    {
        if (!editor.canInteract || !editor.primaryHeld)
        {
            EndStroke();
            return;
        }

        if (!stroke)
        {
            editor.history.BeginGroup();
            stroke = true;
        }

        editor.EraseAt(editor.cell, editor.snappedWorld);
    }

    void EndStroke()
    {
        if (!stroke)
        {
            return;
        }

        editor.history.EndGroup();
        stroke = false;
    }
}

// ----------------------------------------------------------------------
// Select: drag a grid-aligned rectangle. Hold Ctrl to add to the selection.
// Ctrl never switches this mode to the plain mouse.
// ----------------------------------------------------------------------
public class SelectTool : EditorTool
{
    static readonly Color FillColor = new(0.3f, 0.6f, 1f, 0.25f);
    static readonly Color BorderColor = new(0.3f, 0.6f, 1f, 0.9f);

    Vector2I start;
    Vector2I end;
    bool dragging;

    public SelectTool(EditorObject editor) : base(editor) { }

    public override CursorMode mode => CursorMode.Select;
    public override bool isBusy => dragging;
    public override bool allowCtrlMouseOverride => false;

    public override void Enter()
    {
        editor.hideCursor = true;
    }

    public override void Exit()
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        editor.RedrawOverlay();
    }

    public override void Update(double delta)
    {
        if (!dragging)
        {
            if (Input.IsActionJustPressed("mb_left") && editor.canInteract)
            {
                start = end = editor.cell;
                dragging = true;
                editor.RedrawOverlay();
            }

            return;
        }

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            Exit();
            return;
        }

        if (end != editor.cell)
        {
            end = editor.cell;
            editor.RedrawOverlay();
        }

        if (!Input.IsActionPressed("mb_left"))
        {
            dragging = false;
            editor.selection.SelectRect(start, end, additive: Input.IsActionPressed("ctrl"));
            editor.RedrawOverlay();
        }
    }

    public override void Draw(CanvasItem canvas)
    {
        if (!dragging) return;

        var min = new Vector2I(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
        var max = new Vector2I(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));

        Rect2 rect = editor.CellRect(min).Merge(editor.CellRect(max));

        canvas.DrawRect(rect, FillColor, filled: true);
        canvas.DrawRect(rect, BorderColor, filled: false, width: 1.5f);
    }
}

// ----------------------------------------------------------------------
// Move: drag the selection (or whatever is under the cursor) to a new spot.
// Shows a green / red preview, applies on release, Esc cancels.
// ----------------------------------------------------------------------
public class MoveTool : EditorTool
{
    static readonly Color validFill = new(0.3f, 1f, 0.4f, 0.25f);
    static readonly Color validLine = new(0.3f, 1f, 0.4f, 0.95f);
    static readonly Color invalidFill = new(1f, 0.3f, 0.3f, 0.25f);
    static readonly Color invalidLine = new(1f, 0.3f, 0.3f, 0.95f);

    bool dragging;
    bool valid = true;
    Vector2I grabCell;
    Vector2I delta;
    HashSet<Vector2I> otherObjectCells = new();

    public MoveTool(EditorObject editor) : base(editor) { }

    public override CursorMode mode => CursorMode.Move;
    public override bool isBusy => dragging;
    public override bool allowCtrlMouseOverride => !dragging;

    public override void Enter()
    {
        editor.hideCursor = true;
    }

    public override void Exit() => Cancel();

    public override void Update(double delta)
    {
        if (!dragging)
        {
            if (Input.IsActionJustPressed("mb_left") && editor.canInteract)
            {
                Begin();
            }

            return;
        }

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            Cancel();
            return;
        }

        Vector2I newDelta = editor.cell - grabCell;
        bool valid = newDelta == Vector2I.Zero || editor.selection.CanMove(newDelta, otherObjectCells);

        if (newDelta != this.delta || valid != this.valid)
        {
            this.delta = newDelta;
            this.valid = valid;
            editor.RedrawOverlay();
        }

        if (!Input.IsActionPressed("mb_left"))
        {
            if (this.delta != Vector2I.Zero && this.valid)
            {
                editor.selection.TryMove(this.delta);
            }

            Cancel();
        }
    }

    void Begin()
    {
        var selection = editor.selection;

        // Grabbing something outside the selection selects it (honouring the filter) and drags just that.
        if (!selection.HitsSelection(editor.cell))
        {
            if (!selection.TryPick(editor.cell, out Pick pick))
            {
                return;
            }

            selection.SelectOnly(pick);
        }

        dragging = true;
        valid = true;
        grabCell = editor.cell;
        delta = Vector2I.Zero;
        otherObjectCells = selection.OtherObjectCells();
        editor.RedrawOverlay();
    }

    void Cancel()
    {
        if (!dragging) return;

        dragging = false;
        delta = Vector2I.Zero;
        editor.RedrawOverlay();
    }

    public override void Draw(CanvasItem canvas)
    {
        if (!dragging || delta == Vector2I.Zero) return;

        Color fill = valid ? validFill : invalidFill;
        Color line = valid ? validLine : invalidLine;

        foreach (var cell in editor.selection.tiles)
        {
            DrawCell(canvas, cell + delta, fill, line);
        }

        foreach (var obj in editor.selection.objects)
        {
            if (GodotObject.IsInstanceValid(obj))
            {
                DrawCell(canvas, editor.GetObjectCell(obj) + delta, fill, line);
            }
        }
    }

    void DrawCell(CanvasItem canvas, Vector2I cell, Color fill, Color line)
    {
        Rect2 rect = editor.CellRect(cell);

        canvas.DrawRect(rect, fill, filled: true);
        canvas.DrawRect(rect, line, filled: false, width: 1.5f);
    }
}