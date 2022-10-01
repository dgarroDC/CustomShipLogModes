using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace CustomShipLogModes;

[HarmonyPatch]
public class Patches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ShipLogController), nameof(ShipLogController.LateInitialize))]
    private static void ShipLogController_LateInitialize(ShipLogController __instance)
    {
        CustomShipLogModes.Instance.Setup(__instance);
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
                // Replace AllowSwap return value with "false" (top of the stack)
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Ldc_I4_0);
            }
            yield return instruction;
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ShipLogController), nameof(ShipLogController.Update))]
    private static void ShipLogController_Update()
    {
        // We know _initialized is true and so the mod was initialized 
        CustomShipLogModes.Instance.UpdatePromptsVisibility();
        CustomShipLogModes.Instance.UpdateChangeMode();
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ShipLogSplashScreen), nameof(ShipLogSplashScreen.OnEnterComputer))]
    private static void ShipLogSplashScreen_OnEnterComputer()
    {
        // We use ShipLogSplashScreen instead of ShipLogController to get here before the EnterMode
        // (that could add prompts and have a different order after swapping modes)
        CustomShipLogModes.Instance.OnEnterShipComputer();
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ShipLogSplashScreen), nameof(ShipLogSplashScreen.OnExitComputer))]
    private static void ShipLogSplashScreen_OnExitComputer()
    {
        // This could use ShipLogController but we also use ShipLogSplashScreen here for consistency I guess,
        // this way all modes are called to their hooks one upon another, just in case
        CustomShipLogModes.Instance.OnExitShipComputer();
    }
}