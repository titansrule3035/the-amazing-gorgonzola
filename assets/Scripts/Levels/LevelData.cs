using System.Collections.Generic;
using System.Numerics;
using Godot;
using Godot.Collections;

public class LevelData
{
    public const int TileSize = 32;
    public List<LayerData> Layers { get; set; } = new();
    public List<ObjectData> Clones { get; set; } = new();
    public List<ObjectData> KillZones { get; set; } = new();
    public List<ObjectData> OnOffs {  get; set; } = new();
    public List<ObjectData> ClearConditions {  get; set; } = new();
    public List<ObjectData> SemiSolidTiles { get; set; } = new();
}

public class LayerData
{
    public string Name { get; set; } = "";
    public List<TileData> Tiles { get; set; } = new();
}

public class TileData
{
    public int X { get; set; }
    public int Y { get; set; }

    public int SourceId { get; set; }

    public int AtlasX { get; set; }
    public int AtlasY { get; set; }

    public int Alternative { get; set; }
}

public class ObjectData
{
    public string Type { get; set; } = "";

    public Godot.Vector2 Position { get; set; } = Godot.Vector2.Zero;

    public ObjectData(string type, Godot.Vector2 position)
    {
        this.Type = type;
        this.Position = position;
    }
}