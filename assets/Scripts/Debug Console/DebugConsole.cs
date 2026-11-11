using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class DebugConsole : Control
{
    ScrollContainer content;
    RichTextLabel log;

    LineEdit input;

    static DebugConsole instance;

    private static Action<List<string>> LogRead;

    public override void _Ready()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            DebugManager.Log($"DebugConsole instance already exists. Deleting this one...", "DebugConsole");
            QueueFree();
        }
        content = GetNode<ScrollContainer>("scroll_container");

        log = content.GetNode<RichTextLabel>("log");

        input = GetNode<LineEdit>("input_backdrop/line_edit");

        ClearAllText();

        LogRead += OnLogRead;

        ReadLog();

        DebugManager.debug = Runtime.CheckIfDebugKeyExists();

        ScrollToBottom();

        SetActive(false);

        string command = Runtime.GetInstance().launchCommand;

        if (DebugManager.debug && !string.IsNullOrWhiteSpace(command))
        {
            DebugManager.ProcessCommand(command);
        }
    }

    async void ScrollToBottom()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        content.SetDeferred("scroll_vertical", content.GetVScrollBar().MaxValue);
    }

    void WriteToConsole(string text, bool useAuthor = false)
    {
        if (useAuthor)
        {
            log.AddText($"Debug Console: {text}\n");
        }
        else
        {
            log.AddText(text + "\n");
        }

        ScrollToBottom();
    }

    public string GetInputText()
    {
        return input.Text;
    }

    public static DebugConsole GetInstance()
    {
        return instance;
    }

    public void ClearVisibleLog()
    {
        log.Text = "";
    }

    public void ClearInputText()
    {
        input.Text = "";
    }

    public void ClearAllText()
    {
        ClearVisibleLog();
        ClearInputText();
    }

    public override void _ExitTree()
    {
        instance = null;

        LogRead -= OnLogRead;

        base._ExitTree();
    }

    public void SetActive(bool active)
    {
        Visible = active;

        DebugManager.debugConsoleActive = active;

        if (active)
        {
            input.GrabFocus();
        }
        else
        {
            input.ReleaseFocus();
        }
    }

    public static void ReadLog()
    {
        //send the contents of this file into this console's log, separated by line
        List<string> readMessages = new();

        using (StreamReader reader = File.OpenText(Runtime.GetLogPath()))
        {
            string message;
            while ((message = reader.ReadLine()) != null)
            {
                readMessages.Add(message);
            }
        }

        LogRead?.Invoke(readMessages);
    }

    private void OnLogRead(List<string> readMessages)
    {
        ClearVisibleLog();

        foreach (string message in readMessages)
        {
            log.AddText(message + "\n");
        }

        ScrollToBottom();
    }
}
