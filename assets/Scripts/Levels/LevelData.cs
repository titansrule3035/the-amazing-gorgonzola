using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

public class LevelData
{
    public const int TileSize = 32;
    public List<LayerData> Layers { get; set; } = new();
    public List<ObjectData> Clones { get; set; } = new();
    public List<KillZoneData> KillZones { get; set; } = new();
    public OnOffAssetData OnOffs { get; set; } = new();
    public List<ObjectData> ClearConditions { get; set; } = new();
    public List<SemiSolidTileData> SemiSolidTiles { get; set; } = new();

    public static string Encode(object input)
    {
        string text = input.ToString() ?? "";

        // UTF8 -> binary string
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);

        StringBuilder binary1 = new();

        foreach (byte b in utf8Bytes)
        {
            binary1.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
        }

        string r1 = binary1.ToString();

        // Binary string -> Base64
        string r2 = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(r1)
        );

        // Base64 -> binary string
        byte[] base64Bytes = Encoding.UTF8.GetBytes(r2);

        StringBuilder binary2 = new();

        foreach (byte b in base64Bytes)
        {
            binary2.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
        }

        return binary2.ToString();
    }

    public static string Decode(string encoded)
    {
        // Binary string -> Base64 string
        byte[] base64Bytes = new byte[encoded.Length / 8];

        for (int i = 0; i < base64Bytes.Length; i++)
        {
            string chunk = encoded.Substring(i * 8, 8);
            base64Bytes[i] = Convert.ToByte(chunk, 2);
        }

        string base64 = Encoding.UTF8.GetString(base64Bytes);

        // Base64 -> binary string
        string binaryString = Encoding.UTF8.GetString(
            Convert.FromBase64String(base64)
        );

        // Binary string -> UTF8 bytes
        byte[] utf8Bytes = new byte[binaryString.Length / 8];

        for (int i = 0; i < utf8Bytes.Length; i++)
        {
            string chunk = binaryString.Substring(i * 8, 8);
            utf8Bytes[i] = Convert.ToByte(chunk, 2);
        }

        // UTF8 bytes -> original text
        return Encoding.UTF8.GetString(utf8Bytes);
    }
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

    public string Name { get; set; } = "";

    public Godot.Vector2 Position { get; set; } = Godot.Vector2.Zero;

    public ObjectData(string type, string name, Godot.Vector2 position)
    {
        this.Type = type;
        this.Name = name;
        this.Position = position;
    }
}

public class KillZoneData : ObjectData
{
    public Godot.Vector2 Scale { get; set; } = Godot.Vector2.One;

    public KillZoneData(string type, string name, Godot.Vector2 position, Godot.Vector2 scale) : base(type, name, position)
    {
        Scale = scale;
    }
}

public class SemiSolidTileData
{
    public string Type { get; set; } = "";      // normalized — used for scene lookup
    public string Name { get; set; } = "";       // original node name — for identity if needed
    public Godot.Vector2 Position { get; set; } = Godot.Vector2.Zero;
    public Godot.Vector2 Scale { get; set; } = Godot.Vector2.One;

    public SemiSolidTileData(string type, string name, Godot.Vector2 position, Godot.Vector2 scale)
    {
        Type = type;
        Name = name;
        Position = position;
        Scale = scale;
    }

}
public class OnOffSwitchMasterData : ObjectData
{
    public bool Opened { get; set; } = false;

    public OnOffSwitchMasterData(string type, string name, Godot.Vector2 position, bool opened) : base(type, name, position)
    {
        Opened = opened;
    }
}

public class OnOffAssetData
{
    public OnOffSwitchMasterData OnOffSwitchMaster { get; set; }

    public List<ObjectData> OnOffSwitches { get; set; } = new();

    public List<ObjectData> OnOffBlocks { get; set; } = new();
}