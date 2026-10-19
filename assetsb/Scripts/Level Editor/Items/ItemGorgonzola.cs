using Godot;
using System;

public partial class ItemGorgonzola : EditorItemObject
{
    private EditorGameManager gm;

    public override void _Ready()
    {
        base._Ready();
        gm = EditorGameManager.GetInstance();
        if (gm != null)
        {
            // Subscribe to gorg events and set initial state based on whether a gorg already exists
            gm.OnGorgFound += OnGorgFound;
            gm.OnGorgUnregistered += OnGorgUnregistered;

            if (gm.gorgonzola != null)
                DisableItem();
            else
                EnableItem();
        }

    }

    public override void _ExitTree()
    {
        if (gm != null)
        {
            gm.OnGorgFound -= OnGorgFound;
            gm.OnGorgUnregistered -= OnGorgUnregistered;
        }

        base._ExitTree();
    }

    private void OnGorgUnregistered()
    {
        EnableItem();
    }

    private void OnGorgFound()
    {
        DisableItem();
    }
}
