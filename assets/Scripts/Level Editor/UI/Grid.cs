using Godot;

public partial class Grid : Node2D
{
    [Export] public Color lineColor;

    public override void _Process(double delta)
    {
        if (GetTree().Paused)
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (!GetTree().Paused)
            return;

        TileMapLayer tileMapLayer = GetTree().CurrentScene
            .GetNode<TileMapLayer>("level/tiles/Foreground");

        if (tileMapLayer == null)
            return;

        Camera2D camera = GetViewport().GetCamera2D();

        Vector2 topLeft = new(camera.LimitLeft, camera.LimitTop);
        Vector2 bottomRight = new(camera.LimitRight, camera.LimitBottom);

        Vector2I startCell = tileMapLayer.LocalToMap(tileMapLayer.ToLocal(topLeft));
        Vector2I endCell = tileMapLayer.LocalToMap(tileMapLayer.ToLocal(bottomRight));

        Vector2 halfTile = tileMapLayer.TileSet.TileSize / 2;

        for (int x = startCell.X; x <= endCell.X + 1; x++)
        {
            Vector2 a = tileMapLayer.ToGlobal(
                tileMapLayer.MapToLocal(new Vector2I(x, startCell.Y)) - halfTile);

            Vector2 b = tileMapLayer.ToGlobal(
                tileMapLayer.MapToLocal(new Vector2I(x, endCell.Y + 1)) - halfTile);

            DrawLine(ToLocal(a), ToLocal(b), lineColor);
        }

        for (int y = startCell.Y; y <= endCell.Y + 1; y++)
        {
            Vector2 a = tileMapLayer.ToGlobal(
                tileMapLayer.MapToLocal(new Vector2I(startCell.X, y)) - halfTile);

            Vector2 b = tileMapLayer.ToGlobal(
                tileMapLayer.MapToLocal(new Vector2I(endCell.X + 1, y)) - halfTile);

            DrawLine(ToLocal(a), ToLocal(b), lineColor);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPaused || what == NotificationUnpaused)
        {
            QueueRedraw(); // forces immediate update when state changes
        }
    }
}