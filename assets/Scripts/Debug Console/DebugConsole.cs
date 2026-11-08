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

    public override void _Ready()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            GD.PrintErr("DebugConsole instance already exists. Deleting this one...");
            QueueFree();
        }
        content = GetNode<ScrollContainer>("scroll_container");

        log = content.GetNode<RichTextLabel>("log");

        input = GetNode<LineEdit>("input_backdrop/line_edit");

        log.Text = input.Text = "";

        //send the contents of this file into the console log, separated by line

        List<string> readMessages = new();

        using (StreamReader reader = File.OpenText(Runtime.GetLogPath()))
        {
            string message;
            while ((message = reader.ReadLine()) != null)
            {
                GD.Print(message);
                readMessages.Add(message);
            }
        }

        foreach (string message in readMessages)
        {
            log.AddText(message + "\n");
        }

        ScrollToBottom();

        SetActive(false);
    }

    void ScrollToBottom()
    {
        content.SetDeferred("scroll_vertical", log.Size.Y);
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

    public void ClearInputText()
    {
        input.Text = "";
    }

    public override void _ExitTree()
    {
        instance = null;

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
}
