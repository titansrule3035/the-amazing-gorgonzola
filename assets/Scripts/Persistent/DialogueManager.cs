using Godot;
using Ink.Runtime;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DialogueManager : Node
{
    // -------------------------------------------------------------------------
    // Exports (assign in the Godot Inspector)
    // -------------------------------------------------------------------------
    [Export] public Control dialoguePanel;
    [Export] public Label textLabel;
    [Export] public AudioStreamPlayer speechBlip;
    [Export] public float textSpeedDelay = 0.04f;

    // -------------------------------------------------------------------------
    // Public state
    // -------------------------------------------------------------------------
    public bool useTriggerPersistant { get; set; }
    public DialogueTrigger dialogueTrigger { get; set; }

    public enum DialogueMode { Script, Trigger, Null }
    public DialogueMode currentDialogueMode { get; private set; } = DialogueMode.Null;

    public int dialogueNo { get; private set; }
    public bool dialogueIsPlaying { get; private set; }
    public bool skipMode { get; private set; }

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------
    private Story currentStory;
    private bool grabLine;
    private Label[] choicesLabels;

    // Singleton
    private static DialogueManager instance;

    // Cancellation token replacement: a simple flag to abort the typing coroutine
    private bool typingCancelled;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    public override void _Ready()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            GD.PrintErr("More than one instance of InkDialogueManager found! " +
                        "Please ensure there is no other dialogue manager in the scene.");
        }

        grabLine = false;
        dialogueNo = 0;
        useTriggerPersistant = false;
        skipMode = false;
        dialogueIsPlaying = false;
    }

    public override void _Process(double delta)
    {
        if (!dialogueIsPlaying) return;

        if (!grabLine)
        {
            if (Input.IsActionJustPressed("interact"))
            {
                CheckSkipMode();
            }
        }
    }

    // -------------------------------------------------------------------------
    // Enter dialogue — Script mode (no trigger object)
    // -------------------------------------------------------------------------
    public void EnterDialogueMode(string inkJson, AudioStream audioClip)
    {
        GlobalGameManager.GetInstance().canMove = false;
        var file = FileAccess.Open(inkJson, FileAccess.ModeFlags.Read);
        string inkJsonContents = file.GetAsText();
        file.Close();
        currentStory = new Story(inkJsonContents);
        dialogueIsPlaying = true;
        dialoguePanel.Visible = true;
        grabLine = true;
        speechBlip.Stream = audioClip;
        useTriggerPersistant = false;
        currentDialogueMode = DialogueMode.Script;
        ContinueStory();
    }

    // -------------------------------------------------------------------------
    // Enter dialogue — Script mode (no trigger object or audioCLip)
    // -------------------------------------------------------------------------
    public void EnterDialogueMode(string inkJson)
    {
        GlobalGameManager.GetInstance().canMove = false;
        var file = FileAccess.Open(inkJson, FileAccess.ModeFlags.Read);
        string inkJsonContents = file.GetAsText();
        file.Close();
        currentStory = new Story(inkJsonContents);
        dialogueIsPlaying = true;
        dialoguePanel.Visible = true;
        grabLine = true;
        useTriggerPersistant = false;
        currentDialogueMode = DialogueMode.Script;
        ContinueStory();
    }

    // -------------------------------------------------------------------------
    // Enter dialogue — Trigger mode
    // -------------------------------------------------------------------------
    public void EnterDialogueMode(DialogueTrigger dialogueTrigger, string inkJson, AudioStream audioClip)
    {
        GlobalGameManager.GetInstance().canMove = false;
        var file = FileAccess.Open(inkJson, FileAccess.ModeFlags.Read);
        string inkJsonContents = file.GetAsText();
        file.Close();
        currentStory = new Story(inkJsonContents);
        dialogueIsPlaying = true;
        dialoguePanel.Visible = true;
        grabLine = true;
        speechBlip.Stream = audioClip;
        useTriggerPersistant = false;
        this.dialogueTrigger = dialogueTrigger;
        currentDialogueMode = DialogueMode.Trigger;
        ContinueStory();
    }

    // -------------------------------------------------------------------------
    // Enter dialogue — Trigger mode with persistent collider
    // -------------------------------------------------------------------------
    public void EnterDialogueMode(DialogueTrigger dialogueTrigger, string inkJson, AudioStream audioClip, Area2D triggerArea)
    {
        GlobalGameManager.GetInstance().canMove = false;
        var file = FileAccess.Open(inkJson, FileAccess.ModeFlags.Read);
        string inkJsonContents = file.GetAsText();
        file.Close();
        currentStory = new Story(inkJsonContents);
        dialogueIsPlaying = true;
        dialoguePanel.Visible = true;
        speechBlip.Stream = audioClip;
        useTriggerPersistant = true;
        triggerArea.Monitoring = false;   // equivalent to BoxCollider2D.enabled = false
        grabLine = true;
        this.dialogueTrigger = dialogueTrigger;
        currentDialogueMode = DialogueMode.Trigger;
        ContinueStory();
    }

    // -------------------------------------------------------------------------
    // Story flow
    // -------------------------------------------------------------------------
    public void ContinueStory()
    {
        if (currentStory.canContinue)
        {
            typingCancelled = true;   // cancel any running typing task
            grabLine = true;
            dialogueNo++;
            _ = TypeSentenceAsync();   // fire-and-forget async typing
        }
        else
        {
            ExitDialogueMode();
        }
    }

    public void ExitDialogueMode()
    {
        textLabel.Text = string.Empty;
        dialogueNo = 0;

        //GameManager.GetInstance().OnDialogueExit(ChoiceNo, DialogueTrigger, NpcNameLabel.Text, CurrentDialogueMode);
        GlobalGameManager.GetInstance().canMove = true;
        dialoguePanel.Visible = false;

        if (currentDialogueMode == DialogueMode.Trigger && dialogueTrigger != null)
        {
            //if (UseTriggerPersistant && DialogueTrigger.Radius != null)
            //DialogueTrigger.Radius.Monitoring = true;
        }

        dialogueTrigger = null;
        dialogueIsPlaying = false;
        currentDialogueMode = DialogueMode.Null;
    }

    public void CheckSkipMode()
    {
        typingCancelled = true;

        if (skipMode)
        {
            textLabel.Text = currentStory.currentText;
            skipMode = false;
        }
        else
        {
            ContinueStory();
        }
    }

    // -------------------------------------------------------------------------
    // Typewriter effect (async, replaces Unity coroutine)
    // -------------------------------------------------------------------------
    private async Task TypeSentenceAsync()
    {
        typingCancelled = false;
        skipMode = false;
        textLabel.Text = "";

        string line = currentStory.Continue();

        foreach (char letter in line)
        {
            if (typingCancelled)
            {
                return;
            }
            textLabel.Text += letter;
            grabLine = false;

            /*if (textLabel.Text.Length % 2 == 0)
            {
                if (speechBlip.Stream != null)
                {
                    speechBlip.Play();
                }
                skipMode = true;
            }*/
            if (typingCancelled)
            {
                return;
            }
            skipMode = true;
            await ToSignal(GetTree().CreateTimer(textSpeedDelay), SceneTreeTimer.SignalName.Timeout);
        }

        skipMode = false;
    }
    public static DialogueManager GetInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        return null;
    }
}