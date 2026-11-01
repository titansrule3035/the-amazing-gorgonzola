using Godot;
using System;
using System.Collections.Generic;
using CursorMode = EditorObject.CursorMode;

/// <summary>
/// Base class for every cursor mode. EditorObject calls these each frame:
/// UpdateCursor() - always (position / visuals follow the mouse)
/// Update(deltaTime) - only while Editor.EditorActive
/// Draw(canvas) - from the overlay, in WORLD coordinates (call Editor.RedrawOverlay() to refresh)
/// </summary>
public abstract class EditorTool
{
    protected EditorObject editor;

    protected EditorTool(EditorObject editor)
    {
        this.editor = editor;
    }

    /// <summary>The cursor mode this tool implements. Used as the registration key.</summary>
    public abstract CursorMode mode { get; }

    /// <summary>Whether the cursor sprite should be shown in this mode.</summary>
    public virtual bool cursorVisible => false;

    /// <summary>Mid-drag / mid-stroke. Blocks the context menu.</summary>
    public virtual bool isBusy => false;

    /// <summary>Whether holding Ctrl may temporarily switch to the plain mouse.</summary>
    public virtual bool allowCtrlMouseOverride => true;

    /// <summary>The tool became the active one.</summary>
    public virtual void Enter()
    {
    }

    /// <summary>The tool stops being the active one. End any drag or stroke cleanly.</summary>
    public virtual void Exit()
    {
    }

    public virtual void UpdateCursor()
    {
    }

    public virtual void Update(double deltaTime)
    {
    }

    public virtual void Draw(CanvasItem canvas)
    {
    }

    /// <summary>The editor stopped being active (unpaused). End any drag or stroke cleanly.</summary>
    public virtual void OnEditorDeactivated()
    {
        Exit();
    }
}

// ----------------------------------------------------------------------
// Mouse: plain pointer, cursor sprite hidden
// ----------------------------------------------------------------------

/// <summary>Plain pointer. Does nothing except keep the editor node under the mouse.</summary>
public class MouseTool : EditorTool
{
    public MouseTool(EditorObject editor) : base(editor)
    {
    }

    public override CursorMode mode => CursorMode.Mouse;

    public override bool cursorVisible => false;

    public override void UpdateCursor()
    {
        editor.GlobalPosition = editor.mouseGlobal;
    }
}

// ----------------------------------------------------------------------
// Copy / DragCamera: placeholders
// ----------------------------------------------------------------------

/// <summary>Placeholder. Registered so the mode exists, but it has no behaviour yet.</summary>
public class CopyTool : EditorTool
{
    public CopyTool(EditorObject editor) : base(editor)
    {
    }

    public override CursorMode mode => CursorMode.Copy;

    public override bool cursorVisible => false;
}

/// <summary>Placeholder. Registered so the mode exists, but it has no behaviour yet.</summary>
public class DragCameraTool : EditorTool
{
    public DragCameraTool(EditorObject editor) : base(editor)
    {
    }

    public override CursorMode mode => CursorMode.DragCamera;

    public override bool cursorVisible => false;
}

// ----------------------------------------------------------------------
// Place: snaps to the grid, paints tiles / spawns objects while LMB is held.
// One mouse stroke = one undo step.
// ----------------------------------------------------------------------

/// <summary>Paints tiles or spawns objects on the hovered cell while the primary button is held.</summary>
public class PlaceTool : EditorTool
{
    // Sentinel for "no cell placed yet in this stroke".
    static Vector2I noCell => new(int.MinValue, int.MinValue);

    Vector2I lastPlacedCell = noCell;
    bool stroke;

    public PlaceTool(EditorObject editor) : base(editor)
    {
    }

    public override CursorMode mode => CursorMode.Place;

    public override bool cursorVisible => true;

    public override bool isBusy => stroke;

    public override void Enter()
    {
        lastPlacedCell = noCell;
        editor.RefreshCursorItem();
    }

    public override void Exit()
    {
        EndStroke();
    }

    public override void UpdateCursor()
    {
        // A palette item that got disabled (or freed) can't stay selected.
        if (editor.itemObject != null && (!GodotObject.IsInstanceValid(editor.itemObject) || editor.itemObject.disabled))
        {
            editor.ClearEditorItem();
        }
        // Origin sits at the BOTTOM of the hovered tile; the sprite is lifted by half
        // its height so non-tile objects stand on the bottom edge.
        float offset = editor.cursorSprite.Texture != null ? editor.cursorSprite.GetRect().Size.Y / 2f : 0f;
        editor.GlobalPosition = editor.snappedWorld + editor.halfTileDown;
        editor.cursorSprite.Position = new Vector2(0, -offset);
    }

    public override void Update(double deltaTime)
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
        if (!stroke)
        {
            return;
        }
        editor.history.EndGroup();
        stroke = false;
    }
}

// ----------------------------------------------------------------------
// Eraser: follows the raw mouse, erases tile + placed objects under it.
// One mouse stroke = one undo step.
// ----------------------------------------------------------------------

/// <summary>
/// Erases placed objects first, then the tile, under the mouse while the primary button is held.
/// If a cell holds an object and a tile, the object goes first and the tile stays while the eraser
/// remains in that cell. Releasing the button, or leaving and re-entering the cell, erases the tile.
/// </summary>
public class EraserTool : EditorTool
{
    // Sentinel for "no cell where an object was just erased".
    static Vector2I noCell => new(int.MinValue, int.MinValue);

    bool stroke;
    Vector2I objectErasedCell = noCell;

    public EraserTool(EditorObject editor) : base(editor)
    {
    }

    public override CursorMode mode => CursorMode.Eraser;

    public override bool cursorVisible => false;

    public override bool isBusy => stroke;

    public override void Enter()
    {
        editor.SetCursorTexture(null);
        editor.cursorSprite.Material = null;
        editor.cursorSprite.Modulate = Colors.White;
        editor.cursorSprite.Position = Vector2.Zero;
    }

    public override void Exit()
    {
        EndStroke();
    }

    public override void UpdateCursor()
    {
        editor.GlobalPosition = editor.mouseGlobal;
    }

    public override void Update(double deltaTime)
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
        if (editor.cell != objectErasedCell)
        {
            // Left the cell where an object was just erased, so its tile is fair game again.
            objectErasedCell = noCell;
        }
        // Query at the raw mouse position (not the cell center) so anything visibly under the pointer is hit.
        bool removedObject = editor.EraseAt(editor.cell, editor.mouseGlobal, objectErasedCell != editor.cell);
        if (removedObject)
        {
            objectErasedCell = editor.cell;
        }
    }

    void EndStroke()
    {
        objectErasedCell = noCell;
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

/// <summary>Drag a grid-aligned rectangle to select. Hold Ctrl to add to the selection, Esc to cancel.</summary>
public class SelectTool : EditorTool
{
    static Color fillColor => new(0.3f, 0.6f, 1f, 0.25f);
    static Color borderColor => new(0.3f, 0.6f, 1f, 0.9f);

    Vector2I start;
    Vector2I end;
    bool dragging;

    public SelectTool(EditorObject editor) : base(editor)
    {
    }

    public override CursorMode mode => CursorMode.Select;

    public override bool cursorVisible => false;

    public override bool isBusy => dragging;

    public override bool allowCtrlMouseOverride => false;

    public override void Exit()
    {
        if (!dragging)
        {
            return;
        }
        dragging = false;
        editor.RedrawOverlay();
    }

    public override void Update(double deltaTime)
    {
        if (!dragging)
        {
            if (Input.IsActionJustPressed("mb_left") && editor.canInteract)
            {
                start = editor.cell;
                end = editor.cell;
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
        if (!dragging)
        {
            return;
        }
        var min = new Vector2I(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
        var max = new Vector2I(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
        Rect2 rect = editor.CellRect(min).Merge(editor.CellRect(max));
        canvas.DrawRect(rect, fillColor, filled: true);
        canvas.DrawRect(rect, borderColor, filled: false, width: 1.5f);
    }
}

// ----------------------------------------------------------------------
// Move: drag the selection (or whatever is under the cursor) to a new spot.
// Shows a green / red preview, applies on release, Esc cancels.
// ----------------------------------------------------------------------

/// <summary>
/// Drag the selection (or whatever is under the cursor) to a new spot.
/// Shows a green (valid) or red (blocked) preview, applies on release, Esc cancels.
/// </summary>
public class MoveTool : EditorTool
{
    static Color validFill => new(0.3f, 1f, 0.4f, 0.25f);
    static Color validLine => new(0.3f, 1f, 0.4f, 0.95f);
    static Color invalidFill => new(1f, 0.3f, 0.3f, 0.25f);
    static Color invalidLine => new(1f, 0.3f, 0.3f, 0.95f);

    bool dragging;
    bool valid = true;
    Vector2I grabCell;
    Vector2I moveDelta;
    HashSet<Vector2I> otherObjectCells = new();

    public MoveTool(EditorObject editor) : base(editor)
    {
    }

    public override CursorMode mode => CursorMode.Move;

    public override bool cursorVisible => false;

    public override bool isBusy => dragging;

    public override bool allowCtrlMouseOverride => !dragging;

    public override void Exit()
    {
        Cancel();
    }

    public override void Update(double deltaTime)
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
        bool nowValid = newDelta == Vector2I.Zero || editor.selection.CanMoveSelection(newDelta, otherObjectCells);
        if (newDelta != moveDelta || nowValid != valid)
        {
            moveDelta = newDelta;
            valid = nowValid;
            editor.RedrawOverlay();
        }
        if (!Input.IsActionPressed("mb_left"))
        {
            if (moveDelta != Vector2I.Zero && valid)
            {
                editor.selection.TryMove(moveDelta);
            }
            Cancel();
        }
    }

    void Begin()
    {
        EditorSelection selection = editor.selection;
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
        moveDelta = Vector2I.Zero;
        otherObjectCells = selection.OtherObjectCells();
        editor.RedrawOverlay();
    }

    void Cancel()
    {
        if (!dragging)
        {
            return;
        }
        dragging = false;
        moveDelta = Vector2I.Zero;
        editor.RedrawOverlay();
    }

    public override void Draw(CanvasItem canvas)
    {
        if (!dragging || moveDelta == Vector2I.Zero)
        {
            return;
        }
        Color fill = valid ? validFill : invalidFill;
        Color line = valid ? validLine : invalidLine;
        foreach (Vector2I cell in editor.selection.tiles)
        {
            DrawCell(canvas, cell + moveDelta, fill, line);
        }
        foreach (Node2D obj in editor.selection.objects)
        {
            if (GodotObject.IsInstanceValid(obj))
            {
                DrawCell(canvas, editor.GetObjectCell(obj) + moveDelta, fill, line);
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