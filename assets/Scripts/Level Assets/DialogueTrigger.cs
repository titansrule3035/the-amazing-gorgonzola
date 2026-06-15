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
    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    public override void _Ready()
    {
        triggerName = Name;

        hasTransition = ((transitions != null) && (transitions.Length > 0) && (transitions[0] != null));
    }

    public override void _Process(double delta)
    {
        DialogueManager dialogueManager = DialogueManager.GetInstance();
    }

    public void TriggerDialogue()
    {
        DialogueManager.GetInstance().EnterDialogueMode(inkJsonPath);
    }
}