using Godot;

public partial class CameraDrag : Camera2D
{
    [Export] public float DragSpeed = 1.0f;
    [Export] public float MoveSpeed = 500.0f;

    private bool _dragging = false;
    private Vector2 _dragStartMousePos;
    private Vector2 _dragStartCameraPos;

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                if (mouseButton.Pressed)
                {
                    _dragging = true;
                    _dragStartMousePos = GetViewport().GetMousePosition();
                    _dragStartCameraPos = Position;
                }
                else
                {
                    _dragging = false;
                }
            }
        }

        if (@event is InputEventMouseMotion && _dragging)
        {
            Vector2 currentMousePos = GetViewport().GetMousePosition();
            Vector2 delta = (currentMousePos - _dragStartMousePos) * Zoom;
            Position = _dragStartCameraPos - delta * DragSpeed;
        }
    }

    public override void _Process(double delta)
    {
        Vector2 move = Vector2.Zero;

        if (Input.IsKeyPressed(Godot.Key.I))
            move.Y -= 1;

        if (Input.IsKeyPressed(Godot.Key.K))
            move.Y += 1;

        if (Input.IsKeyPressed(Godot.Key.J))
            move.X -= 1;

        if (Input.IsKeyPressed(Godot.Key.L))
            move.X += 1;

        if (move != Vector2.Zero)
        {
            move = move.Normalized();
            Position += move * MoveSpeed * (float)delta;
        }

        if (Input.IsKeyPressed(Godot.Key.F))
            GlobalPosition = new(-224, -400);
    }
}