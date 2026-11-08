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
    public static DebugCommand<int, int> SET;
    public static DebugCommand<int, int, int> SET_AT;
    public static DebugCommand<int, int> ADD;
    public static DebugCommand<int, int> FLOOD;
    public static DebugCommand<int, int> REMOVE;
    public static DebugCommand<int> REMOVE_ALL;
    public static DebugCommand<int, int, int> REMOVE_AT;
    public static DebugCommand<int, int> DROP;
    public static DebugCommand CLEAR_INV;
    public static DebugCommand DELETE_STRAY_ITEMS;
    public static DebugCommand EXIT_DIALOGUE;
    public static DebugCommand<string> LOG_WORD;
    public static DebugCommand HELP;
    public static DebugCommand CLEAR_CONSOLE;
    public static bool typing = false;
    public static List<object> commandList;
    public static void BuildCommands()
    {
        SET = new DebugCommand<int, int>(".set", "Sets all items in inventory to the given item ID.", ".set <itemID> <amount>", (x, y) =>
        {
            // function here
        });
        SET_AT = new DebugCommand<int, int, int>(".set_at", "Sets the given index in the inventory to the given item ID.", ".set_at <index> <itemID> <amount>", (i, x, y) =>
        {
            // function here
        });
        ADD = new DebugCommand<int, int>(".add", "Adds the given item ID into the next available inventory slot.", ".add <itemID> <amount>", (x, y) =>
        {
            // function here
        });
        FLOOD = new DebugCommand<int, int>(".flood", "Floods the given item ID into all available inventory slots.", ".flood <itemID> <amount>", (x, y) =>
        {
            // function here
        });
        REMOVE = new DebugCommand<int, int>(".remove", "Removes the given item ID from the inventory starting at the end.", ".remove <itemID> <amount>", (x, y) =>
        {
            // function here
        });
        REMOVE_ALL = new DebugCommand<int>(".remove_all", "Removes all instances of the given item ID from the inventory.", ".remove_all <itemID>", (x) =>
        {
            // function here
        });
        REMOVE_AT = new DebugCommand<int, int, int>(".remove_at", "Removes the given item ID from the inventory using the given index.", ".remove_at <index> <itemID> <amount>", (i, x, y) =>
        {
            // function here
        });
        DROP = new DebugCommand<int, int>(".drop", "Drops whatever item is in the given index, assuming it is not empty.", ".drop <index> <inventoryPageIndex>", (i, d) =>
        {
            // function here
        });
        CLEAR_INV = new DebugCommand(".clear_inv", "Clears all items from the player's inventory.", ".clear_inv", () =>
        {
            // function here
        });
        DELETE_STRAY_ITEMS = new DebugCommand(".delete_stray_items", "Deletes all items from the scene that are not in the inventory.", ".delete_stray_items", () =>
        {
            // function here
        });
        EXIT_DIALOGUE = new DebugCommand(".exit_dialogue", "Closes the dialogue box and exits dialogue mode.", ".exit_dialogue", () =>
        {
            // function here
        });
        LOG_WORD = new DebugCommand<string>(".log_word", "Logs a single word into the console.", ".log_word <word>", (s) =>
        {
            // function here
        });
        HELP = new DebugCommand(".help", "Displays all commands in the console.", ".help", () =>
        {
            // function here
        });
        CLEAR_CONSOLE = new DebugCommand(".clear_console", "Clears the debug console and wipes the logging file.", ".clear_console", () =>
        {
            // function here

        });
        // register commands into a list for easy access
        commandList = new List<object> {
      SET,
      SET_AT,
      ADD,
      FLOOD,
      REMOVE,
      REMOVE_ALL,
      REMOVE_AT,
      DROP,
      CLEAR_INV,
      DELETE_STRAY_ITEMS,
      EXIT_DIALOGUE,
      LOG_WORD,
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
        string[] properties = commandLine.Split(' ');
        for (int i = 0; i < commandList.Count; i++)
        {
            DebugCommandBase debugCommandBase = commandList[i] as DebugCommandBase;

            if (commandLine.Contains(" "))
            {
                if (debugCommandBase.CommandID == commandLine.Substring(0, commandLine.IndexOf(' ')))
                {
                    if (commandList[i] as DebugCommand != null)
                    {
                        (commandList[i] as DebugCommand).Invoke();
                    }
                    else if (commandList[i] as DebugCommand<int> != null)
                    {
                        (commandList[i] as DebugCommand<int>).Invoke(int.Parse(properties[1]));
                    }
                    else if (commandList[i] as DebugCommand<int, int> != null)
                    {
                        (commandList[i] as DebugCommand<int, int>).Invoke(int.Parse(properties[1]), int.Parse(properties[2]));
                    }
                }
            }
            else
            {
                if (debugCommandBase.CommandID == commandLine.Substring(0, commandLine.IndexOf(' ')))
                {
                    if (commandList[i] as DebugCommand != null)
                    {
                        (commandList[i] as DebugCommand).Invoke();
                    }
                    else if (commandList[i] as DebugCommand<int> != null)
                    {
                        (commandList[i] as DebugCommand<int>).Invoke(int.Parse(properties[1]));
                    }
                    else if (commandList[i] as DebugCommand<int, int> != null)
                    {
                        (commandList[i] as DebugCommand<int, int>).Invoke(int.Parse(properties[1]), int.Parse(properties[2]));
                    }
                }
            }
        }
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
    }
    public static string GetCurrentDateTimeFormatted()
    {
        DateTime now = DateTime.Now;
        string formattedDateTime = now.ToString("dd/MM/yyyy - HH:mm:ss");
        return formattedDateTime;
    }
    public static void Log(object input)
    {
        StackFrame callStack = new(1, true);
        CollectDebugMessage($" {GetCurrentDateTimeFormatted()}: {input}");
    }
    public static void PlainLog(object input)
    {
        CollectDebugMessage($" {input}");
    }
}