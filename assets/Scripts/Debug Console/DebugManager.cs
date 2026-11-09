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
    public static DebugCommand<int, int> GOTO_LEVEL;
    public static DebugCommand LOAD_CACHED_LEVEL;
    public static DebugCommand HELP;
    public static DebugCommand CLEAR_CONSOLE;
    public static bool typing = false;
    public static List<DebugCommandBase> commandList;
    public static void BuildCommands()
    {
        LOG = new DebugCommand<string>("log", "Logs a string into the console.", "log <string>", (s) =>
        {
            Log(s, "User");
        });
        DUMP_LOG = new DebugCommand<string>("dump_log", "Logs a string into the console and dumps it into your documents folder.", "dump_log <string>", (s) =>
        {
            Log(s, "User", true);
        });
        GOTO_LEVEL = new DebugCommand<int, int>("goto_level", "Allows you to game levels from a given group and TAGLevel index.", "goto_level <group_group> <TAGLevelIndex>", (groupIndex, TAGLevelIndex) =>
        {
            if (!Runtime.editorMode)
            {
                GlobalGameManager.GetInstance().OnLevelLoaded += Runtime.GetInstance().OnLevelSkipped;

                GlobalGameManager.GetInstance().LoadLevel(groupIndex, TAGLevelIndex);
            }
            else
            {
                DebugManager.Log($"Command {GOTO_LEVEL.CommandID} can only be executed in game mode, switch to main menu and try again.", "DebugManagerService");
            }
        });
        LOAD_CACHED_LEVEL = new DebugCommand("load_cached_level", "Loads the cached temporary level in the level editor, can only execute this command in editor mode.", "load_cached_level", () =>
        {
            if (Runtime.editorMode)
            {
                // find a way to statically load levels
                Runtime.GetInstance().FilePicked(Path.Combine(OS.GetUserDataDir(), "tmp/.taglevel"));
            }
            else
            {
                Log($"Command {LOAD_CACHED_LEVEL.CommandID} can only be executed in editor mode, switch to editor and try again.", "DebugManagerService");
            }
        });
        HELP = new DebugCommand("help", "Displays all commands in the console.", "help", () =>
        {
            PlainLog("List of available commands:");
            for (int i = 0; i < commandList.Count; i++)
            {
                PlainLog("\n");
                PlainLog($" * {commandList[i].CommandID} - {commandList[i].CommandDescription}");
                PlainLog($"   * Example: {commandList[i].CommandFormat}");
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
      GOTO_LEVEL,
      LOAD_CACHED_LEVEL,
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
        Log($"Unknown command: {commandID}", "DebugManager");
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
        string formattedDateTime = now.ToString("dd-MM-yyyy_HH-mmss");
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

    public static void Log(object input, string owner, bool dumpLog = false)
    {
        StackFrame callStack = new(1, true);
        string message;
        if (String.IsNullOrWhiteSpace(owner))
        {
            message = $"{callStack} {GetCurrentDateTimeFormatted()}: {input}";
        }
        else
        {
            message = $"<{owner}>: {input}";
        }
        CollectDebugMessage(message);

        if (dumpLog)
        {
            using Godot.FileAccess file = Godot.FileAccess.Open(Runtime.CreateLogDump(), Godot.FileAccess.ModeFlags.Write);

            file.StoreString(message);
        }
    }

    public static void PlainLog(object input, bool dumpLog = false)
    {
        CollectDebugMessage(input.ToString());

        if (dumpLog)
        {
            using (StreamWriter writer = File.AppendText(Runtime.CreateLogDump()))
            {
                writer.WriteLine(input);
            }
        }
    }
}