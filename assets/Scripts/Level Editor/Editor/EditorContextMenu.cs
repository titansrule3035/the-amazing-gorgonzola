using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ======================================================================
// Public extension points
// ======================================================================

/// <summary>
/// Implement this on a placeable item's script to add options to its right-click menu:
///
///   public void BuildContextMenu(EditorContextMenuBuilder menu)
///   {
///       menu.AddUndoablePerObject("Flip", n => Flip(n), n => Flip(n));
///   }
///
/// You can also register options by item type without touching the item's script:
/// see EditorContextRegistry and EditorObject.RegisterItemContextOptions().
/// </summary>
public interface IEditorContextProvider
{
    void BuildContextMenu(EditorContextMenuBuilder menu);
}

public sealed class EditorContextOption
{
    public string Label = "";
    public Action? Callback;
    public bool Disabled;
    public bool Separator;
}

/// <summary>Passed to providers so they can add options for the object that was right-clicked.</summary>
public sealed class EditorContextMenuBuilder
{
    internal readonly List<EditorContextOption> Options = new();

    public EditorObject Editor { get; }

    /// <summary>The placed object that was right-clicked, or null if it was a tile / empty space.</summary>
    public Node2D? Target { get; }

    public Vector2I Cell { get; }

    public EditorContextMenuBuilder(EditorObject editor, Node2D? target, Vector2I cell)
    {
        Editor = editor;
        Target = target;
        Cell = cell;
    }

    /// <summary>Selected objects of the same item type as Target (so options apply to the whole selection).</summary>
    public IReadOnlyList<Node2D> SelectedOfSameType
    {
        get
        {
            if (Target == null) return Array.Empty<Node2D>();

            var type = Editor.GetObjectType(Target);
            return Editor.Selection.Objects.Where(o => Editor.GetObjectType(o) == type).ToList();
        }
    }

    /// <summary>Plain option. NOT undoable; use AddUndoable for anything that changes the level.</summary>
    public void Add(string label, Action callback, bool disabled = false)
    {
        Options.Add(new EditorContextOption { Label = label, Callback = callback, Disabled = disabled });
    }

    public void AddSeparator()
    {
        if (Options.Count == 0 || Options[^1].Separator) return;
        Options.Add(new EditorContextOption { Separator = true });
    }

    /// <summary>Option that goes through undo/redo.</summary>
    public void AddUndoable(string label, Action doIt, Action undoIt, bool disabled = false)
    {
        Add(label, () => Editor.History.Execute(new ActionCommand(doIt, undoIt)), disabled);
    }

    /// <summary>Undoable option applied to every selected object of the target's type.</summary>
    public void AddUndoablePerObject(string label, Action<Node2D> doOne, Action<Node2D> undoOne, bool disabled = false)
    {
        var targets = SelectedOfSameType.ToList();

        AddUndoable(
            label,
            () => { foreach (var n in targets) if (GodotObject.IsInstanceValid(n)) doOne(n); },
            () => { foreach (var n in targets) if (GodotObject.IsInstanceValid(n)) undoOne(n); },
            disabled);
    }
}

/// <summary>Register context menu options for an item type without editing the item's own script.</summary>
public static class EditorContextRegistry
{
    static readonly Dictionary<EditorItemObject.ItemType, List<Action<EditorContextMenuBuilder>>> Providers = new();

    public static void Clear() => Providers.Clear();

    public static void Register(EditorItemObject.ItemType type, Action<EditorContextMenuBuilder> provider)
    {
        if (!Providers.TryGetValue(type, out var list))
        {
            Providers[type] = list = new List<Action<EditorContextMenuBuilder>>();
        }

        list.Add(provider);
    }

    internal static void Apply(EditorItemObject.ItemType type, EditorContextMenuBuilder builder)
    {
        if (Providers.TryGetValue(type, out var list))
        {
            foreach (var provider in list) provider(builder);
        }
    }
}

// ======================================================================
// The menu itself
// ======================================================================

public sealed class EditorContextMenu
{
    readonly EditorObject editor;
    readonly List<EditorContextOption> options = new();

    public PopupMenu Menu { get; }

    public EditorContextMenu(EditorObject editor)
    {
        this.editor = editor;

        Menu = new PopupMenu { ProcessMode = Node.ProcessModeEnum.Always };
        Menu.IdPressed += OnIdPressed;
        Menu.VisibilityChanged += () =>
        {
            if (!Menu.Visible) editor.MarkUiInteraction();
        };
    }

    public void Open()
    {
        var selection = editor.Selection;
        Vector2I cell = editor.cell;

        // Right-clicking something that isn't selected selects it first (like most editors).
        bool hit = selection.TryPick(cell, out Pick pick);

        if (hit && !selection.Contains(pick))
        {
            selection.SelectOnly(pick);
        }

        Node2D? target = hit ? pick.Object : null;
        var builder = new EditorContextMenuBuilder(editor, target, cell);

        // 1. Item-specific options
        if (target != null)
        {
            if (target is IEditorContextProvider provider)
            {
                provider.BuildContextMenu(builder);
            }

            EditorContextRegistry.Apply(editor.GetObjectType(target), builder);
        }

        // 2. Selection options
        builder.AddSeparator();

        if (!selection.IsEmpty)
        {
            builder.Add($"Delete ({selection.Count})", editor.DeleteSelection);
        }

        if (target != null)
        {
            var type = editor.GetObjectType(target);
            builder.Add("Select All of This Type", () => selection.SelectAllOfType(type));
        }

        if (!selection.IsEmpty)
        {
            builder.Add("Deselect All", selection.Clear);
        }

        // 3. History
        builder.AddSeparator();
        builder.Add("Undo", editor.Undo, !editor.History.canUndo);
        builder.Add("Redo", editor.Redo, !editor.History.canRedo);

        Show(builder.Options);
    }

    void Show(List<EditorContextOption> options)
    {
        this.options.Clear();
        this.options.AddRange(options);

        Menu.Clear();

        for (int i = 0; i < this.options.Count; i++)
        {
            var option = this.options[i];

            if (option.Separator)
            {
                Menu.AddSeparator();
                continue;
            }

            Menu.AddItem(option.Label, i);

            if (option.Disabled)
            {
                Menu.SetItemDisabled(Menu.ItemCount - 1, true);
            }
        }

        var viewport = editor.GetViewport();
        Vector2I position = viewport.IsEmbeddingSubwindows() ? (Vector2I)viewport.GetMousePosition() : DisplayServer.MouseGetPosition();

        Menu.Popup(new Rect2I(position, Vector2I.Zero));
    }

    void OnIdPressed(long id)
    {
        int index = (int)id;

        if (index >= 0 && index < this.options.Count)
        {
            this.options[index].Callback?.Invoke();
        }
    }
}