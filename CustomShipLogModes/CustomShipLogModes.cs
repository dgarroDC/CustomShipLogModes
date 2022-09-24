using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OWML.ModHelper;

namespace CustomShipLogModes;

[HarmonyPatch]
public class CustomShipLogModes : ModBehaviour
{
    public static CustomShipLogModes Instance;
   
    private static ModSelectorMode _modSelectorMode;
    private static bool init;

    private ShipLogController _shipLogController;
    private ShipLogMode _goBackMode;

    // TODO: Move Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/ScreenPromptListScaleRoot/ TO LAST CHILD
    // TODO: Add "C" (detective/map) and "F" prompts (custom/menu) make visible when needed
    private void Start()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Instance = this;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ShipLogController), nameof(ShipLogController.Update))]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // We want to do the "mode swap" logic ourselves in the postfix
        // Is the transpiler really necessary? Probably not
        bool found = false;
        bool replaceNext = false;
        foreach (var instruction in instructions)
        {
            if (!found && instruction.Calls(typeof(ShipLogMode).GetMethod("AllowModeSwap")))
            {
                found = true;
                replaceNext = true;
            }
            else if (replaceNext)
            {
                replaceNext = false;
                // Replace AllowSwap return value with "false"
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Ldc_I4_0);
            }
            yield return instruction;
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ShipLogController), nameof(ShipLogController.Update))]
    private static void ShipLogController_Update(ShipLogController __instance)
    {
        // TODO: MOve!
        if (!init)
        {
            Instance._shipLogController = __instance;
            _modSelectorMode = new ModSelectorMode();
            // TODO: Save the prompt lists
            _modSelectorMode.Initialize(__instance._centerPromptList, __instance._upperRightPromptList,
                __instance._oneShotSource);
            init = true;
        }
    
        Instance.UpdateChangeMode();
    }

    private void UpdateChangeMode()
    {
        ShipLogMode currentMode = _shipLogController._currentMode;
        if (currentMode.AllowModeSwap())
        {
            if (Input.IsNewlyPressed(Input.Action.OpenModeSelector))
            {
                // TODO: < 2 cases
                _goBackMode = currentMode;
                ChangeMode(_modSelectorMode);
            }
            else if (Input.IsNewlyPressed(Input.Action.SwapMode))
            {
                ShipLogMode mapMode = _shipLogController._mapMode;
                ShipLogMode detectiveMode = _shipLogController._detectiveMode;
                if (currentMode == mapMode)
                {
                    // We know detective mode is enabled because AllowModeSwap
                    ChangeMode(detectiveMode);
                }
                else if (currentMode == detectiveMode)
                {
                    ChangeMode(mapMode);
                }
                else  // TODO: Check only custom mode?
                {
                    ChangeMode(PlayerData.GetDetectiveModeEnabled() ? detectiveMode : mapMode);
                }
            }
        }
        if (currentMode == _modSelectorMode && Input.IsNewlyPressed(Input.Action.CloseModeSelector) && _goBackMode != null)
        {
            // Check null just in case this mode wasn't opened from the expected path
            // I would like to move this to ModSelectorMode.UpdateMode, but UpdateMode is called before the postfix and could reopen the menu 
            // TODO: Don't show the prompt in that case
            ChangeMode(_goBackMode);
        }
    }

    private void ChangeMode(ShipLogMode enteringMode)
    {
        ShipLogMode leavingMode = _shipLogController._currentMode;
        string focusedEntryID = leavingMode.GetFocusedEntryID();
        leavingMode.ExitMode();
        _shipLogController._currentMode = enteringMode;
        _shipLogController._currentMode.EnterMode(focusedEntryID);
        // This is done originally done in ShipLogController.Update but we are preventing it with the transpiler
        // Other modes should implement the sound inside EnterMode if they want to
        if (_shipLogController._currentMode is ShipLogMapMode)
        {
            _shipLogController._oneShotSource.PlayOneShot(AudioType.ShipLogEnterMapMode);
        }
        else if (_shipLogController._currentMode is ShipLogDetectiveMode)
        {
            _shipLogController._oneShotSource.PlayOneShot(AudioType.ShipLogEnterDetectiveMode);
        }
    }
}