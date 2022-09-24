using System;
using System.Collections.Generic;

namespace CustomShipLogModes;

// Stolen from my mod Suit Log but with other actions
public static class Input
{
    public enum Action
    {
        ListUp,
        ListDown,
        OpenModeSelector,
        CloseModeSelector
    }

    private static List<IInputCommands> GetInputCommands(Action action)
    {
        switch (action)
        {
            case Action.ListUp:
                return new List<IInputCommands>{InputLibrary.up, InputLibrary.up2};
            case Action.ListDown:
                return new List<IInputCommands>{InputLibrary.down, InputLibrary.down2};
            case Action.OpenModeSelector:
                return new List<IInputCommands>{InputLibrary.flashlight};
            case Action.CloseModeSelector:
                return new List<IInputCommands>{InputLibrary.cancel, InputLibrary.flashlight};
        }

        return null;
    }

    private static bool CheckAction(Action action, Func<IInputCommands, bool> checker)
    {
        foreach (IInputCommands commands in GetInputCommands(action))
        {
            if (checker.Invoke(commands))
            {
                return true;
            }
        }

        return false;
    }

    public static IInputCommands PromptCommands(Action action)
    {
        return GetInputCommands(action)[0];
    }

    public static bool IsNewlyPressed(Action action)
    {
        return CheckAction(action, commands => OWInput.IsNewlyPressed(commands));
    }

    public static bool IsPressed(Action action)
    {
        return CheckAction(action, commands => OWInput.IsPressed(commands));
    }

    public static float GetValue(Action action)
    {
        List<IInputCommands> commands = GetInputCommands(action);
        return OWInput.GetValue(commands[0]);
    }
}