using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static System.Runtime.CompilerServices.RuntimeHelpers;

public static partial class DebugManager
{

    public static bool debug = false;
    public static bool debugConsoleActive = false;

    public static List<string> debugMessages = new();
    public static DebugCommand<string> LOG;
    public static DebugCommand<string> DUMP_LOG;
    public static DebugCommand HELP;
    public static DebugCommand CLEAR_CONSOLE;
    public static bool typing = false;
    public static List<DebugCommandBase> commandList;
    public static void BuildCommands()
    {
        LOG = new DebugCommand<string>("log", "Logs a string into the console.", "log <string>", (s) =>
        {
            Log(s);
        });
        DUMP_LOG = new DebugCommand<string>("dump_log", "Logs a string into the console and dumps it into your documents folder.", "dump_log <string>", (s) =>
            {
                Log(s, true);
            });
        HELP = new DebugCommand("help", "Displays all commands in the console.", "help", () =>
        {
            Log("List of available commands:");
            for (int i = 0; i < commandList.Count; i++)
            {
                Log($"{commandList[i].CommandID} - {commandList[i].CommandDescription}");
                Log($"Example: {commandList[i].CommandFormat}");
            }
        });
        CLEAR_CONSOLE = new DebugCommand("clear_console", "Clears the debug console and wipes the logging file.", "clear_console", () =>
        {
            debugMessages = new();
            StreamWriter writer = new(Runtime.GetLogPath());
            writer.WriteLine("");
            writer.Close();
            DebugConsole.ReadLog();

        });
        // register commands into a list for easy access
        commandList = new List<DebugCommandBase> {
      LOG,
      DUMP_LOG,
      HELP,
      CLEAR_CONSOLE
    };
    }

    public static void ToggleDebug()
    {
        if (debug)
        {
            debug = false;
        }
        else
        {
            debug = true;
        }
    }
    public static void ToggleDebugConsole()
    {
        DebugConsole.GetInstance().SetActive(true);
    }

    public static void ProcessCommand(string commandLine)
    {
        // Extract command ID and arguments
        int spaceIndex = commandLine.IndexOf(' ');

        string commandID = spaceIndex == -1 ? commandLine : commandLine.Substring(0, spaceIndex);

        string arguments = spaceIndex == -1 ? "" : commandLine.Substring(spaceIndex + 1).Trim();

        string[] properties = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < commandList.Count; i++)
        {
            DebugCommandBase debugCommandBase = commandList[i] as DebugCommandBase;

            if (debugCommandBase.CommandID != commandID)
            {
                continue;
            }

            if (commandList[i] is DebugCommand debugCommand)
            {
                debugCommand.Invoke();
            }
            else if (commandList[i] is DebugCommand<string> stringCommand)
            {
                stringCommand.Invoke(arguments);
            }
            else if (commandList[i] is DebugCommand<int> intCommand)
            {
                if (properties.Length < 1 || !int.TryParse(properties[0], out int value))
                {
                    return;
                }

                intCommand.Invoke(value);
            }
            else if (commandList[i] is DebugCommand<int, int> twoIntCommand)
            {
                if (properties.Length < 2 || !int.TryParse(properties[0], out int value1) || !int.TryParse(properties[1], out int value2))
                {
                    return;
                }

                twoIntCommand.Invoke(value1, value2);
            }
            else if (commandList[i] is DebugCommand<int, int, int> threeIntCommand)
            {
                if (properties.Length < 3 || !int.TryParse(properties[0], out int value1) || !int.TryParse(properties[1], out int value2) || !int.TryParse(properties[2], out int value3))
                {
                    return;
                }

                threeIntCommand.Invoke(value1, value2, value3);
            }

            return;
        }

        // No matching command found
        Log($"Unknown command: {commandID}");
    }

    public static void CollectDebugMessage(string input)
    {
        string[] readMessages = File.ReadAllLines(Runtime.GetLogPath());
        string outputText = "";
        debugMessages = readMessages.ToList();
        debugMessages.Add(input);
        using (StreamWriter writer = File.AppendText(Runtime.GetLogPath()))
        {
            writer.WriteLine(input);
        }

        foreach (string message in debugMessages)
        {
            outputText = message + "\n" + outputText;
        }

        DebugConsole.ReadLog();
    }



    public static string GetCurrentDateTimeFormatted()
    {
        DateTime now = DateTime.Now;
        string formattedDateTime = now.ToString("dd-MM-yyyy_HH-mm-ss");
        return formattedDateTime;
    }
    public static void Log(object input, bool dumpLog = false)
    {
        StackFrame callStack = new(1, true);
        string message = $" {callStack} {GetCurrentDateTimeFormatted()}: {input}";
        CollectDebugMessage(message);

        if (dumpLog)
        {
            using Godot.FileAccess file = Godot.FileAccess.Open(Runtime.CreateLogDump(), Godot.FileAccess.ModeFlags.Write);

            file.StoreString(message);
        }
    }
    public static void PlainLog(object input, bool dumpLog = false)
    {
        CollectDebugMessage($" {input}");

        if (dumpLog)
        {
            using (StreamWriter writer = File.AppendText(Runtime.CreateLogDump()))
            {
                writer.WriteLine(input);
            }
        }
    }
}