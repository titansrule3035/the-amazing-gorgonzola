using Godot;
using System.Threading.Tasks;

public partial class DialogueTrigger : Node2D
{
    // -------------------------------------------------------------------------
    // Exports
    // -------------------------------------------------------------------------
    [Export] public AudioStream speechClip;
    [Export(PropertyHint.File, "*.json")] public string inkJsonPath;
    [Export] public DialogueTrigger[] transitions = System.Array.Empty<DialogueTrigger>();

    // -------------------------------------------------------------------------
    // Public state (read by DialogueManager)
    // -------------------------------------------------------------------------
    public string triggerName { get; private set; }
    public bool hasTransition { get; private set; }

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------
    private bool lockBool;
    private string inkContents;
    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    public override void _Ready()
    {
        triggerName = Name;

        hasTransition = ((transitions != null) && (transitions.Length > 0) && (transitions[0] != null));

        var file = FileAccess.Open(inkJsonPath, FileAccess.ModeFlags.Read);
        inkContents = file.GetAsText();
        file.Close();
    }

    public override void _Process(double delta)
    {
        DialogueManager dialogueManager = DialogueManager.GetInstance();
        if (Input.IsActionJustPressed("jump"))
        {
            dialogueManager.EnterDialogueMode(inkJsonPath);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private string LoadInkJson()
    {
        if (string.IsNullOrEmpty(inkJsonPath))
        {
            GD.PrintErr($"InkDialogueTrigger '{Name}': inkJsonPath is not set.");
            return null;
        }

        if (!FileAccess.FileExists(inkJsonPath))
        {
            GD.PrintErr($"InkDialogueTrigger '{Name}': file not found at '{inkJsonPath}'.");
            return null;
        }

        return FileAccess.GetFileAsString(inkJsonPath);
    }
}