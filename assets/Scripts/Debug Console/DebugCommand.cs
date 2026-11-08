using System;
using System.Collections;
using System.Collections.Generic;

public class DebugCommandBase
{
    private readonly string _commandID;
    private readonly string _commandDescription;
    private readonly string _commandFormat;

    public string CommandID
    {
        get
        {
            return _commandID;
        }
    }
    public string CommandDescription
    {
        get
        {
            return _commandDescription;
        }
    }
    public string CommandFormat
    {
        get
        {
            return _commandFormat;
        }
    }
    public DebugCommandBase(string id, string description, string format)
    {
        _commandID = id;
        _commandDescription = description;
        _commandFormat = format;
    }
}
public class DebugCommand : DebugCommandBase
{
    private readonly Action command;
    public DebugCommand(string id, string description, string format, Action command) : base(id, description, format)
    {
        this.command = command;
    }
    public void Invoke()
    {
        command.Invoke();
    }
}
public class DebugCommand<T1> : DebugCommandBase
{
    private readonly Action<T1> command;
    public DebugCommand(string id, string description, string format, Action<T1> command) : base(id, description, format)
    {
        this.command = command;
    }
    public void Invoke(T1 value)
    {
        command.Invoke(value);
    }
}
public class DebugCommand<T1, T2> : DebugCommandBase
{
    private readonly Action<T1, T2> command;
    public DebugCommand(string id, string description, string format, Action<T1, T2> command) : base(id, description, format)
    {
        this.command = command;
    }
    public void Invoke(T1 val1, T2 val2)
    {
        command.Invoke(val1, val2);
    }
}
public class DebugCommand<T1, T2, T3> : DebugCommandBase
{
    private readonly Action<T1, T2, T3> command;
    public DebugCommand(string id, string description, string format, Action<T1, T2, T3> command) : base(id, description, format)
    {
        this.command = command;
    }
    public void Invoke(T1 val1, T2 val2, T3 val3)
    {
        command.Invoke(val1, val2, val3);
    }
}