using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;

namespace CustomShipLogModes;

[HarmonyPatch]
public class CustomShipLogModes : ModBehaviour
{
    private static ModSelectorMode _modSelectorMode;
    private static ShipLogMode _goBackMode;
    private static bool init;
    public static CustomShipLogModes Instance;
    
    // TODO: Move Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/ScreenPromptListScaleRoot/ TO LAST CHILD

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
            _modSelectorMode = new ModSelectorMode();
            // TODO: Save the prompt lists
            _modSelectorMode.Initialize(__instance._centerPromptList, __instance._upperRightPromptList,
                __instance._oneShotSource);
            init = true;
        }
    
        if (__instance._currentMode.AllowModeSwap() && Input.IsNewlyPressed(Input.Action.OpenModeSelector))
        {
            ShipLogMode enteringMode;
            if (__instance._currentMode != _modSelectorMode)
            {
                enteringMode = _modSelectorMode;
                _goBackMode = __instance._currentMode; // TODO: Move to ChangeMode?
            }
            else
            {
                enteringMode = _goBackMode;
            }
            ChangeMode(__instance, _modSelectorMode);
        }
        // TODO: Swap key
    }

    // TODO: Keep shipLogController in field?
    private static void ChangeMode(ShipLogController shipLogController,ShipLogMode enteringMode)
    {
        ShipLogMode leavingMode = shipLogController._currentMode;
        string focusedEntryID = leavingMode.GetFocusedEntryID();
        leavingMode.ExitMode();
        shipLogController._currentMode = enteringMode;
        shipLogController._currentMode.EnterMode(focusedEntryID);
        // This is done originally done in ShipLogController.Update but we are preventing it
        // Other modes should implement the sound inside EnterMode if they want to
        if (shipLogController._currentMode is ShipLogMapMode)
        {
            shipLogController._oneShotSource.PlayOneShot(AudioType.ShipLogEnterMapMode);
        }
        else if (shipLogController._currentMode is ShipLogDetectiveMode)
        {
            shipLogController._oneShotSource.PlayOneShot(AudioType.ShipLogEnterDetectiveMode);
        }
    }
}