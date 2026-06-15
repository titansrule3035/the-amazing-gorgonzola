using Godot;
using System.Threading.Tasks;

public partial class DialogueTrigger : Node2D
{
    // Editor exports
    // Audio clip played when this trigger starts a dialogue.
    [Export] public AudioStream speechClip;
    // Path to the Ink JSON file used by the DialogueManager.
    [Export(PropertyHint.File, "*.json")] public string inkJsonPath;
    // Optional transitions to other DialogueTrigger nodes in the scene.
    [Export] public DialogueTrigger[] transitions = System.Array.Empty<DialogueTrigger>();

    // Runtime
    // Name of this trigger, set during _Ready().
    public string triggerName { get; private set; }
    // True if at least one valid transition is assigned.
    public bool hasTransition { get; private set; }

    // Simple lock to prevent re-entrancy when triggering dialogues.
    private bool lockBool;
    
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