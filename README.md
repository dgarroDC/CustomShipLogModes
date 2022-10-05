# Custom Ship Log Modes by Damián Garro

![thumbnail](images/thumbnail.webp)

This utility mod allows other mods to add their custom Ship Log modes. Now you can [kill demons](https://outerwildsmods.com/mods/doom/), [visit other stars](https://outerwildsmods.com/mods/newhorizons/) or even view your gallery of photos (*coming soon?*) on your computer without worrying about compatibility issues!

## User Guide

The mod adds a prompt to open a menu to select all the custom and vanilla modes (custom modes are at the top sorted alphabetically and the vanilla modes are at the bottom) using the flashlight key (**F key** by default on the keyboard):

![menu example](images/menu-example.webp)

You can also cycle through the modes using the secondary interact key (**C key** by default on the keyboard):

![cycle example](images/cycle-example.webp)

**Note:** If you prefer the Outer Wilds behavior of the secondary interact key, you can disable the *"Cycle through modes"* option in the settings: Pressing the key in the Map Mode would switch to Rumor Mode and vice versa. In the case of a custom mode, pressing the key would change the mode to the default one (Rumor Mode of Map Mode if the former is disabled).  

## How to add modes in your mod

### The API

Include this interface in your mod:
```cs
public interface ICustomShipLogModesAPI
{
    public void AddMode(ShipLogMode mode, Func<bool> isEnabledSupplier, Func<string> nameSupplier);
}
```

And use the API to add your mode:
```csharp
var customModesAPI = ModHelper.Interaction.TryGetModApi<ICustomShipLogModesAPI>("dgarro.CustomShipLogModes");
customModesAPI.AddMode(doomMode, () => true, () => "DOOM");
```

You pass your mode itself with the `mode` parameter. With the `isEnabledSupplier` parameter you indicate if the mode is enabled (meaning that the mode is selectable) and with `nameSupplier` the name of the mode (using in prompts and the mode selector menu). You can enable/disable the mode or change its name on the fly with these suppliers, so there's no need to call again `AddMode` for that, for example:

```csharp
// Add the mode but it is disabled for now
Main.HasWarpDrive = false;
API.AddMode(StarChartHandler.ShipLogStarChartMode, 
            () => Main.HasWarpDrive, 
            () => TranslationHandler.GetTranslation("INTERSTELLAR_MODE", TranslationHandler.TextType.UI));

[...]            

// Now you can select the mode!
Main.HasWarpDrive = true;
```

### The ShipLogMode class

Your mode is a `ShipLogMode`. This abstract class is included in the vanilla game and the Map Mode and Rumor Mode implement them (`ShipLogMapMode` and `ShipLogDetectiveMode`). The mode selector menu that this mod uses was is also a `ShipLogMode` (`ModSelectorMode`). Below are the methods that you need to implement (override) in your mode. You don't call these methods, they are called by the base game or by this utility mod on certain events, you "only" have to implement them. In the following explanation it is described when these methods are called and examples of actions you can do on them.

---
 `public void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)`

This is called when you add your mode is added (using `AddMode` in the API) if the `ShipLogController` (vanilla component) was initialized (`LateInitialize()` was called on it). If you added the mode before that, the method will be called just after the `ShipLogController` initialization takes place. You probably don't have to worry about that detail. The method is called even if your mode is disabled. 

You can do whatever you want in this method (even nothing, you can leave it empty in that case), for example creating your prompts, UI elements and any initialization you desire. You can also keep the parameters in fields so you can use it later, `centerPromptList` and `upperRightPromptList` are the prompt lists that are already used in vanilla to add some prompts in different positions, and you can use `oneShotSource` to play sounds.

---
`public void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)`

This is called when your mode is selected, if this is called you now know that your mode is the currently selected one, congratulations! You should probably show your UI on the computer screen and add prompts here. Another thing you could do is play a sound here if you want.

The `entryID` parameter is the ID of the `ShipLogEntry` that was selected in the previous mode that is exiting (the result of `GetFocusedEntryID()` of that mode) or the empty string (`""`) if none was selected. You are probably not dealing with the actual Ship Log, in that case, you can just ignore it, this is used in the base game to swap between Map and Rumor Mode and keep the same selected entry. The `revealQueue` includes the newly revealed Ship Log facts, but this is mostly `null` or empty because when you enter the computer if the list is not empty then the computer will enter the Rumor Mode by default (unless this is disabled in the settings) to show the cool animations of new things, just ignore that parameter.

---
`public void ExitMode()`

This is called when exiting your mode (to exit the computer or change to another mode). Hide all your UI elements and remove prompts here, but please don't play sounds when this is called! Other modes could play a sound on `EnterMode`, so if the mode is changing from yours to another one, if you play a sound in `ExitMode` then it would overlap with the `EnterMode` sound of the other mode and that would be annoying, so I'm **IMPOSING** this convention (although I have no power and you can ignore the convention, but please don't!). 

---
`public void OnEnterComputer()`

This is called on all modes when the player enters the computer. Here you can do for example some initialization of stuff that could only change between computer sessions, but you would probably ignore this method and leave it empty.

---
`public void OnExitComputer()`

This is called on all modes when the player exits the computer. If your mode was the selected mode, this method is called after `ExitMode` was called, you don't have to worry about hiding UI in that case. Again, you may just ignore this method.

---
`public void UpdateMode()`

This is called each frame where your mode is the current one selected. You can get the input of the user here and act upon it, update UI and animations, and anything you want.

---
`public bool AllowModeSwap()`

Return `true` here if you allow changing to another mode (for example by pressing F or C) while your mode is the current one selected. In most implementations, you always return `true` here, but you could return `false` if for example you are waiting for an animation or some process to finish, but don't forget to return `true` eventually, or else the player would be stuck in your mode!

---
`public bool AllowCancelInput()`

Return `true` here if you allow exiting the computer by pressing the cancel key (**Q key** by default on the keyboard) while your mode is the current one selected. Again, you could return `false` in similar cases to `AllowModeSwap`, but another useful case here is when you are inside a menu withing your mode, and you want the cancel key to exit the menu instead of the computer (this is something that the `ShipLogMapMode` does for example).

---
`public string GetFocusedEntryID()`

This is called when changing from your mode to another one. Return here the ID of the selected `ShipLogEntry` in your mode, or `""` if none is selected (this string will be passed to the next mode with `EnterMode` as explained in that method). In other words, just return `""` here, unless your mode is actually dealing with selecting Ship Log entries. But please don't return random strings or `null`, that would make the vanilla `ShipLogMapMode` and `ShipLogDetectiveMode` fail on `EnterMode` because they assume that the string is either empty or an existing entry ID.

---

Here I provide you an example `ShipLogMode` class called `NoOpMode` that basically does "nothing" (or return the expected default values), you could copy it, rename it and change only the methods that you really want to use (most probably `EnterMode` and `ExitMode`, also `Initialize` and `UpdateMode`):

```csharp
public class NoOpMode : ShipLogMode
{
    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        // No-op
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        // No-op
    }

    public override void ExitMode()
    {
        // No-op
    }

    public override void OnEnterComputer()
    {
        // No-op
    }

    public override void OnExitComputer()
    {
        // No-op
    }

    public override void UpdateMode()
    {
        // No-op
    }

    public override bool AllowModeSwap()
    {
        return true;
    }

    public override bool AllowCancelInput()
    {
        return true;
    }

    public override string GetFocusedEntryID()
    {
        return "";
    }
}
```
