using Godot;
using System;
using System.Threading.Tasks;
[GlobalClass]
[Icon("res://assets/Sprites/Program/icon.svg")]
public partial class LevelGroup : Resource
{
    [Export] public PackedScene Scene { get; set; }
    [Export(PropertyHint.File, "*.taglevel")] public Godot.Collections.Array<string> TagLevels { get; set; } = new();

    public bool HasTAGLEVELs()
    {
        return GetTAGLEVELCount() > 0;
    }

    public int GetTAGLEVELCount()
    {
        return TagLevels.Count;
    }

    public string GetTAGLEVELName(int index)
    {
        if (index < 0 || index >= TagLevels.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
        }
        return TagLevels[index].GetFile().GetBaseName();
    }
}