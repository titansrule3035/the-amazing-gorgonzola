using Godot;
using static EditorObject;

public partial class CameraDrag : Camera2D
{
    [Export] public float DragSpeed = 1.0f;
    [Export] public float MoveSpeed = 500.0f;

    private bool _dragging = false;
    private Vector2 _dragStartMousePos;
    private Vector2 _dragStartCameraPos;

    private Vector2 _storedCameraPos;

    private static CameraDrag instance;

    public bool canDrag = false;

    public override void _Ready()
    {
        instance = this;

        base._Ready();
    }
    public override void _Process(double delta)
    {
        EditorObject editorObject = GetTree().CurrentScene.GetNode<EditorObject>("EditorObject");

        Vector2 move = Vector2.Zero;

        if (Input.IsKeyPressed(Godot.Key.I))
        {
            move.Y -= 1;
        }

        if (Input.IsKeyPressed(Godot.Key.K))
        {
            move.Y += 1;
        }

        if (Input.IsKeyPressed(Godot.Key.J))
        {
            move.X -= 1;
        }

        if (Input.IsKeyPressed(Godot.Key.L))
        {
            move.X += 1;
        }

        if (move != Vector2.Zero)
        {
            move = move.Normalized();
            Position += move * MoveSpeed * (float)delta;
            ClampToBounds();
        }

        if (Input.IsKeyPressed(Godot.Key.F))
        {
            GlobalPosition = new(-224, -400);
        }

        if (Input.IsActionJustPressed("spacebar") || Input.IsActionJustPressed("mb_middle"))
        {
            canDrag = true;

            if (editorObject.currentCursorMode != EditorObject.CursorMode.DragCamera)
            {
                editorObject.lastCursorMode = editorObject.currentCursorMode;
            }

            editorObject.SetCursorMode(EditorObject.CursorMode.DragCamera);
        }

        if (Input.IsActionJustReleased("spacebar") || Input.IsActionJustReleased("mb_middle"))
        {
            canDrag = _dragging = false;

            editorObject.SetCursorMode(editorObject.lastCursorMode);
        }

        if (canDrag)
        {
            _dragStartMousePos = GetViewport().GetMousePosition();
            _dragStartCameraPos = Position;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (((mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed && Input.IsActionPressed("spacebar")) || (mouseButton.ButtonIndex == MouseButton.Middle && mouseButton.Pressed)))
            {
                _dragging = true;
            }
            else
            {
                _dragging = false;
            }
        }

        if (@event is InputEventMouseMotion && _dragging && canDrag)
        {
            Vector2 currentMousePos = GetViewport().GetMousePosition();
            Vector2 delta = (currentMousePos - _dragStartMousePos) * Zoom;
            Position = _dragStartCameraPos - delta * DragSpeed;
            ClampToBounds();
        }
    }

    public void StorePos()
    {
        _storedCameraPos = Position;
    }

    public void RestorePos()
    {
        Position = _storedCameraPos;
    }

    public static CameraDrag GetInstance()
    {
        return instance;
    }
    private void ClampToBounds()
    {
        Vector2 halfView = GetViewportRect().Size * Zoom * 0.5f;

        Position = new Vector2(
            Mathf.Clamp(Position.X,
                LimitLeft + halfView.X,
                LimitRight - halfView.X),
            Mathf.Clamp(Position.Y,
                LimitTop + halfView.Y,
                LimitBottom - halfView.Y)
        );
    }
}