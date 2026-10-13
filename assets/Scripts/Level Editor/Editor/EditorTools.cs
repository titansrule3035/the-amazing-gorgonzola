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
    protected readonly EditorObject Editor;

    protected EditorTool(EditorObject editor) => Editor = editor;

    public abstract CursorMode Mode { get; }

    /// <summary>Whether the cursor sprite should be shown in this mode.</summary>
    public virtual bool CursorVisible => false;

    /// <summary>Mid-drag / mid-stroke. Blocks the context menu.</summary>
    public virtual bool IsBusy => false;

    /// <summary>Whether holding Ctrl may temporarily switch to the plain mouse.</summary>
    public virtual bool AllowCtrlMouseOverride => true;

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void UpdateCursor() { }
    public virtual void Update(double delta) { }
    public virtual void Draw(CanvasItem canvas) { }

    /// <summary>The editor stopped being active (unpaused). End any drag or stroke cleanly.</summary>
    public virtual void OnEditorDeactivated() => Exit();
}

// ----------------------------------------------------------------------
// Mouse: plain pointer, cursor sprite hidden
// ----------------------------------------------------------------------
public class MouseTool : EditorTool
{
    public MouseTool(EditorObject editor) : base(editor) { }

    public override CursorMode Mode => CursorMode.Mouse;

    public override void UpdateCursor() => Editor.GlobalPosition = Editor.mouseGlobal;
}

// ----------------------------------------------------------------------
// Copy / DragCamera: placeholders
// ----------------------------------------------------------------------
public class CopyTool : EditorTool
{
    public CopyTool(EditorObject editor) : base(editor) { }

    public override CursorMode Mode => CursorMode.Copy;
}

public class DragCameraTool : EditorTool
{
    public DragCameraTool(EditorObject editor) : base(editor) { }

    public override CursorMode Mode => CursorMode.DragCamera;
}

// ----------------------------------------------------------------------
// Place: snaps to the grid, paints tiles / spawns objects while LMB is held.
// One mouse stroke = one undo step.
// ----------------------------------------------------------------------
public class PlaceTool : EditorTool
{
    static readonly Vector2I NoCell = new(int.MinValue, int.MinValue);

    Vector2I _lastPlacedCell = NoCell;
    bool _stroke;

    public PlaceTool(EditorObject editor) : base(editor) { }

    public override CursorMode Mode => CursorMode.Place;
    public override bool CursorVisible => Editor.HasPlaceableItem;
    public override bool IsBusy => _stroke;

    public override void Enter()
    {
        _lastPlacedCell = NoCell;
        Editor.RefreshCursorItem();
    }

    public override void Exit() => EndStroke();

    public override void UpdateCursor()
    {
        if (Editor.itemObject != null && Editor.itemObject.disabled)
        {
            Editor.ClearEditorItem();
        }

        // Origin sits at the BOTTOM of the hovered tile; the sprite is lifted by half
        // its height so non-tile objects stand on the bottom edge.
        float offset = Editor.CursorSprite.Texture != null
            ? Editor.CursorSprite.GetRect().Size.Y / 2f
            : 0f;

        Editor.GlobalPosition = Editor.snappedWorld + Editor.HalfTileDown;
        Editor.CursorSprite.Position = new Vector2(0, -offset);
    }

    public override void Update(double delta)
    {
        if (!Editor.CanInteract || !Editor.PrimaryHeld)
        {
            EndStroke();
            _lastPlacedCell = NoCell;
            return;
        }

        if (!Editor.HasPlaceableItem) return;
        if (Editor.cell == _lastPlacedCell) return;
        if (Editor.IsHoveredCellBlocked()) return;

        if (!_stroke)
        {
            Editor.History.BeginGroup();
            _stroke = true;
        }

        _lastPlacedCell = Editor.cell;
        Editor.PlaceCurrentItem();
    }

    void EndStroke()
    {
        if (!_stroke) return;

        Editor.History.EndGroup();
        _stroke = false;
    }
}

// ----------------------------------------------------------------------
// Eraser: follows the raw mouse, erases tile + placed objects under it.
// One mouse stroke = one undo step.
// ----------------------------------------------------------------------
public class EraserTool : EditorTool
{
    bool _stroke;

    public EraserTool(EditorObject editor) : base(editor) { }

    public override CursorMode Mode => CursorMode.Eraser;
    public override bool CursorVisible => true;
    public override bool IsBusy => _stroke;

    public override void Enter()
    {
        Editor.SetCursorTexture(Editor.EraserTexture);
        Editor.CursorSprite.Material = null;
        Editor.CursorSprite.Modulate = Colors.White;
        Editor.CursorSprite.Position = Vector2.Zero;
    }

    public override void Exit() => EndStroke();

    public override void UpdateCursor() => Editor.GlobalPosition = Editor.mouseGlobal;

    public override void Update(double delta)
    {
        if (!Editor.CanInteract || !Editor.PrimaryHeld)
        {
            EndStroke();
            return;
        }

        if (!_stroke)
        {
            Editor.History.BeginGroup();
            _stroke = true;
        }

        Editor.EraseAt(Editor.cell, Editor.snappedWorld);
    }

    void EndStroke()
    {
        if (!_stroke) return;

        Editor.History.EndGroup();
        _stroke = false;
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

    Vector2I _start;
    Vector2I _end;
    bool _dragging;

    public SelectTool(EditorObject editor) : base(editor) { }

    public override CursorMode Mode => CursorMode.Select;
    public override bool IsBusy => _dragging;
    public override bool AllowCtrlMouseOverride => false;

    public override void Exit()
    {
        if (!_dragging) return;

        _dragging = false;
        Editor.RedrawOverlay();
    }

    public override void Update(double delta)
    {
        if (!_dragging)
        {
            if (Input.IsActionJustPressed("mb_left") && Editor.CanInteract)
            {
                _start = _end = Editor.cell;
                _dragging = true;
                Editor.RedrawOverlay();
            }

            return;
        }

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            Exit();
            return;
        }

        if (_end != Editor.cell)
        {
            _end = Editor.cell;
            Editor.RedrawOverlay();
        }

        if (!Input.IsActionPressed("mb_left"))
        {
            _dragging = false;
            Editor.Selection.SelectRect(_start, _end, additive: Input.IsActionPressed("ctrl"));
            Editor.RedrawOverlay();
        }
    }

    public override void Draw(CanvasItem canvas)
    {
        if (!_dragging) return;

        var min = new Vector2I(Math.Min(_start.X, _end.X), Math.Min(_start.Y, _end.Y));
        var max = new Vector2I(Math.Max(_start.X, _end.X), Math.Max(_start.Y, _end.Y));

        Rect2 rect = Editor.CellRect(min).Merge(Editor.CellRect(max));

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
    static readonly Color ValidFill = new(0.3f, 1f, 0.4f, 0.25f);
    static readonly Color ValidLine = new(0.3f, 1f, 0.4f, 0.95f);
    static readonly Color InvalidFill = new(1f, 0.3f, 0.3f, 0.25f);
    static readonly Color InvalidLine = new(1f, 0.3f, 0.3f, 0.95f);

    bool _dragging;
    bool _valid = true;
    Vector2I _grabCell;
    Vector2I _delta;
    HashSet<Vector2I> _otherObjectCells = new();

    public MoveTool(EditorObject editor) : base(editor) { }

    public override CursorMode Mode => CursorMode.Move;
    public override bool IsBusy => _dragging;
    public override bool AllowCtrlMouseOverride => !_dragging;

    public override void Exit() => Cancel();

    public override void Update(double delta)
    {
        if (!_dragging)
        {
            if (Input.IsActionJustPressed("mb_left") && Editor.CanInteract)
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

        Vector2I newDelta = Editor.cell - _grabCell;
        bool valid = newDelta == Vector2I.Zero || Editor.Selection.CanMove(newDelta, _otherObjectCells);

        if (newDelta != _delta || valid != _valid)
        {
            _delta = newDelta;
            _valid = valid;
            Editor.RedrawOverlay();
        }

        if (!Input.IsActionPressed("mb_left"))
        {
            if (_delta != Vector2I.Zero && _valid)
            {
                Editor.Selection.TryMove(_delta);
            }

            Cancel();
        }
    }

    void Begin()
    {
        var selection = Editor.Selection;

        // Grabbing something outside the selection selects it (honouring the filter) and drags just that.
        if (!selection.HitsSelection(Editor.cell))
        {
            if (!selection.TryPick(Editor.cell, out Pick pick)) return;
            selection.SelectOnly(pick);
        }

        _dragging = true;
        _valid = true;
        _grabCell = Editor.cell;
        _delta = Vector2I.Zero;
        _otherObjectCells = selection.OtherObjectCells();
        Editor.RedrawOverlay();
    }

    void Cancel()
    {
        if (!_dragging) return;

        _dragging = false;
        _delta = Vector2I.Zero;
        Editor.RedrawOverlay();
    }

    public override void Draw(CanvasItem canvas)
    {
        if (!_dragging || _delta == Vector2I.Zero) return;

        Color fill = _valid ? ValidFill : InvalidFill;
        Color line = _valid ? ValidLine : InvalidLine;

        foreach (var cell in Editor.Selection.Tiles)
        {
            DrawCell(canvas, cell + _delta, fill, line);
        }

        foreach (var obj in Editor.Selection.Objects)
        {
            if (GodotObject.IsInstanceValid(obj))
            {
                DrawCell(canvas, Editor.GetObjectCell(obj) + _delta, fill, line);
            }
        }
    }

    void DrawCell(CanvasItem canvas, Vector2I cell, Color fill, Color line)
    {
        Rect2 rect = Editor.CellRect(cell);

        canvas.DrawRect(rect, fill, filled: true);
        canvas.DrawRect(rect, line, filled: false, width: 1.5f);
    }
}