using Godot;
using Ink.Runtime;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DialogueManager : Node
{
    // UI Elements
    [Export] public Control dialoguePanel;
    [Export] public Label textLabel;
    [Export] public Control dialogueArrow;
    [Export] public AudioStreamPlayer speechBlip;
    [Export] public float textSpeedDelay = 0.025f;

    // Dialogue settings / state
    [Export] public int lineLimit = 31;
    public bool useTriggerPersistant { get; set; }
    public DialogueTrigger dialogueTrigger { get; set; }

    public enum DialogueMode { Script, Trigger, Null }
    public DialogueMode currentDialogueMode { get; private set; } = DialogueMode.Null;

    public int dialogueNo { get; private set; }
    public bool dialogueIsPlaying { get; private set; }
    public bool skipMode { get; private set; }

    int typingGeneration = 0;
    private string displayText = "";
    private Story currentStory;
    private bool grabLine;
    private Label[] choicesLabels;

    private static DialogueManager instance;

    private bool typingCancelled;

    public override void _Ready()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            GD.PrintErr("More than one instance of DialogueManager found! " + "Please ensure there is no other dialogue manager in the scene.");
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

        dialoguePanel.Size = new Vector2(dialoguePanel.Size.X, textLabel.Size.Y + 34);

        if (!grabLine)
        {
            if (Input.IsActionJustPressed("mouse_click_left"))
            {
                CheckSkipMode();
            }
        }
    }


    // Enter dialogue — Script mode (no trigger object)
    public void EnterDialogueMode(string inkJson, AudioStream audioClip)
    {
        StartDialogue(null, inkJson, audioClip, null);
    }

    // Enter dialogue — Script mode (no trigger object or audioCLip)
    public void EnterDialogueMode(string inkJson)
    {
        StartDialogue(null, inkJson, null, null);
    }
    // Enter dialogue — Trigger mode
    public void EnterDialogueMode(DialogueTrigger dialogueTrigger, string inkJson, AudioStream audioClip)
    {
        StartDialogue(dialogueTrigger, inkJson, audioClip, null);
    }

    // Enter dialogue — Trigger mode with persistent collider
    public void EnterDialogueMode(DialogueTrigger dialogueTrigger, string inkJson, AudioStream audioClip, Area2D triggerArea)
    {
        StartDialogue(dialogueTrigger, inkJson, audioClip, triggerArea);
    }
    private void StartDialogue(DialogueTrigger dialogueTrigger, string inkJson, AudioStream audioClip, Area2D triggerArea)
    {
        GlobalGameManager.GetInstance().canMove = false;
        var file = FileAccess.Open(inkJson, FileAccess.ModeFlags.Read);
        currentStory = new Story(file.GetAsText());
        file.Close();
        dialogueIsPlaying = true;
        dialoguePanel.Visible = true;
        if (audioClip != null)
        {
            speechBlip.Stream = audioClip;
        }
        if (triggerArea != null)
        {
            useTriggerPersistant = true;
            triggerArea.Monitoring = false;
        }
        grabLine = true;
        if (dialogueTrigger != null)
        {
            this.dialogueTrigger = dialogueTrigger;
            currentDialogueMode = DialogueMode.Trigger;
        }
        ContinueStory();
    }
    private void ContinueStory()
    {
        dialogueArrow.Visible = false;
        if (currentStory.canContinue)
        {
            displayText = WrapText(currentStory.Continue(), lineLimit);
            typingCancelled = true;
            grabLine = true;
            dialogueNo++;
            _ = TypeSentenceAsync();
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
            dialogueArrow.Visible = true;
            textLabel.Text = displayText;
            skipMode = false;
        }
        else
        {
            ContinueStory();
        }
    }

    private async Task TypeSentenceAsync()
    {
        int myGeneration = ++typingGeneration;
        typingCancelled = false;
        skipMode = false;
        textLabel.Text = "";

        for (int i = 0; i < displayText.Length; i++)
        {
            if (myGeneration != typingGeneration || typingCancelled)
                return;

            textLabel.Text += displayText[i];

            if (displayText[i] == '!' || displayText[i] == '?' || displayText[i] == '.')
            {
                await ToSignal(GetTree().CreateTimer(textSpeedDelay * 10), SceneTreeTimer.SignalName.Timeout);
            }
            else if (displayText[i] == ',')
            {
                await ToSignal(GetTree().CreateTimer(textSpeedDelay * 5), SceneTreeTimer.SignalName.Timeout);
            }

            grabLine = false;
            skipMode = true;

            await ToSignal(
                GetTree().CreateTimer(textSpeedDelay),
                SceneTreeTimer.SignalName.Timeout);

            if (myGeneration != typingGeneration || typingCancelled)
                return;
        }

        dialogueArrow.Visible = true;
        skipMode = false;
    }

    private string WrapText(string text, int lineLimit)
    {
        string[] words = text.Split(' ');
        string result = "";
        int currentLineLength = 0;

        foreach (string word in words)
        {
            if (currentLineLength > 0 &&
                currentLineLength + 1 + word.Length > lineLimit)
            {
                result += "\n";
                currentLineLength = 0;
            }
            else if (currentLineLength > 0)
            {
                result += " ";
                currentLineLength++;
            }

            result += word;
            currentLineLength += word.Length;
        }

        return result;
    }

    public static DialogueManager GetInstance()
    {
        if (instance != null)
        {
            return instance;
        }
        return null;
    }

    public async void HidePanel()
    {
        dialoguePanel.Visible = false;
    }

    public void ShowPanel()
    {
        dialoguePanel.Visible = true;
    }

}