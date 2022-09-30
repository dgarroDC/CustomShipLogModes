using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using OWML.ModHelper;
using UnityEngine;

namespace CustomShipLogModes;

[HarmonyPatch]
public class CustomShipLogModes : ModBehaviour
{
    public static CustomShipLogModes Instance;

    private ModSelectorMode _modSelectorMode;
    private Dictionary<ShipLogMode, Tuple<Func<bool>, Func<string>>> _modes = new();

    private static ShipLogController _shipLogController;
    private ShipLogMode _goBackMode;

    private ScreenPrompt _modeSelectorPrompt;
    private ScreenPrompt _modeSwapPrompt;

    // TODO: Move Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/ScreenPromptListScaleRoot/ TO LAST CHILD
    // TODO: Add "C" (detective/map) and "F" prompts (custom/menu) make visible when needed 
    // TODO: Check null custom modes, clean
    private void Start()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Instance = this;
        LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
    }

    private void OnCompleteSceneLoad(OWScene originalScene, OWScene loadScene)
    {
        _shipLogController = null;
        // Don't clean _modes, maybe a mod added a mode before this?, "nulls" (destroyed) will be ignored for now
    }
    
    public void Setup(ShipLogController shipLogController)
    {
        SetupPrompts();
        _shipLogController = FindObjectOfType<ShipLogController>();
        
        GameObject mapModeGo = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode");
        GameObject selectorModeGo = Instantiate(mapModeGo, mapModeGo.transform.position, mapModeGo.transform.rotation, mapModeGo.transform.parent);
        _modSelectorMode = selectorModeGo.AddComponent<ModSelectorMode>();
        InitializeMode(_modSelectorMode);

        AddMode(shipLogController._mapMode, () => true, () => UITextLibrary.GetString(UITextType.LogMapModePrompt));
        AddMode(shipLogController._detectiveMode, () => PlayerData.GetDetectiveModeEnabled(), () => UITextLibrary.GetString(UITextType.LogMapModePrompt));
        AddMode(_modSelectorMode, () => true, () => UITextLibrary.GetString(UITextType.LogMapModePrompt));
        AddMode(_modSelectorMode, () => true, () => UITextLibrary.GetString(UITextType.LogMapModePrompt));
    }

    private void SetupPrompts()
    {
        // The text is updated
        _modeSelectorPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.OpenModeSelector), "");
        _modeSwapPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.SwapMode), "");
    }

    private void InitializeMode(ShipLogMode mode)
    {
        bool canvasActive = _shipLogController._shipLogCanvas.gameObject.activeSelf;
        _shipLogController._shipLogCanvas.gameObject.SetActive(true);
        mode.Initialize(_shipLogController._centerPromptList, _shipLogController._upperRightPromptList, _shipLogController._oneShotSource);
        _shipLogController._shipLogCanvas.gameObject.SetActive(canvasActive);
    }

    private void AddMode(ShipLogMode mode, Func<bool> isEnabledSupplier, Func<string> nameSupplier)
    {
        if (_modes.ContainsKey(mode))
        {
            ModHelper.Console.WriteLine("Mode " + mode + " already added, replacing suppliers...");
        }
        _modes[mode] = new Tuple<Func<bool>, Func<string>>(isEnabledSupplier, nameSupplier);
    }

    public void UpdatePromptsVisibility()
    {
        ShipLogMode currentMode = _shipLogController._currentMode;
        List<ShipLogMode> customModes = GetCustomModes();
        bool isCustomMode = customModes.Contains(currentMode);

        // TODO: AllowSwap, position odd (Back to Map Mode -> Select Mode)
        _modeSelectorPrompt.SetVisibility((IsVanillaMode(currentMode) && customModes.Count >= 1)
                                          || (isCustomMode && customModes.Count >= 2));
        _modeSelectorPrompt.SetText(customModes.Count == 1? GetModeName(customModes[0]) : "Select Mode");

        _modeSwapPrompt.SetVisibility(isCustomMode); // Vanilla modes add their own prompts
        _modeSwapPrompt.SetText(PlayerData.GetDetectiveModeEnabled()? 
            UITextLibrary.GetString(UITextType.LogRumorModePrompt) : 
            UITextLibrary.GetString(UITextType.LogMapModePrompt));
    }
 
    internal void UpdateChangeMode()
    {
        // TODO: Use same for UpdatePromptsVisibility and UpdateChangeMode?
        ShipLogMode currentMode = _shipLogController._currentMode;
        List<ShipLogMode> customModes = GetCustomModes();
        bool isCustomMode = customModes.Contains(currentMode);
        
        if (currentMode.AllowModeSwap())
        {
            if (_modeSelectorPrompt.IsVisible() && Input.IsNewlyPressed(Input.Action.OpenModeSelector))
            {
                if (customModes.Count == 1)
                {
                    ChangeMode(customModes[0]);
                    return;
                }
                _goBackMode = currentMode;
                ChangeMode(_modSelectorMode);
                return;
            }

            if (Input.IsNewlyPressed(Input.Action.SwapMode))
            {
                // Don't check _modeSwapPrompt.IsVisible (because of vanilla cases)
                ShipLogMode mapMode = _shipLogController._mapMode;
                ShipLogMode detectiveMode = _shipLogController._detectiveMode;
                if (currentMode == mapMode)
                {
                    // We know detective mode is enabled because AllowModeSwap
                    ChangeMode(detectiveMode);
                    return;
                }
                if (currentMode == detectiveMode)
                {
                    ChangeMode(mapMode);
                    return;
                }
                if (isCustomMode)
                {
                    ChangeMode(PlayerData.GetDetectiveModeEnabled() ? detectiveMode : mapMode);
                    return;   
                }
            }
        }
        if (currentMode == _modSelectorMode && Input.IsNewlyPressed(Input.Action.CloseModeSelector) && _goBackMode != null)
        {
            // Check null just in case this mode wasn't opened from the expected path
            // I would like to move this to ModSelectorMode.UpdateMode, but UpdateMode is called before the postfix and could reopen the menu 
            // TODO: Don't show the prompt in that case
            ChangeMode(_goBackMode); // It could be inactive but ok
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

    private bool IsVanillaMode(ShipLogMode mode)
    {
        return mode == _shipLogController._mapMode || mode == _shipLogController._detectiveMode;
    }

    private List<ShipLogMode> GetCustomModes()
    {
        List<ShipLogMode> customModes = new List<ShipLogMode>();
        foreach (var (mode, tuple) in _modes)
        {
            if (mode != null && !IsVanillaMode(mode) && tuple.Item1.Invoke())
            {
                customModes.Add(mode);
            }
        }
        return customModes;
    }

    private string GetModeName(ShipLogMode mode)
    {
        return _modes[mode].Item2.Invoke();
    }

    public void OnEnterShipComputer()
    {
        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.AddScreenPrompt(_modeSwapPrompt, _shipLogController._upperRightPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_modeSelectorPrompt, _shipLogController._upperRightPromptList, TextAnchor.MiddleRight);
    }

    public void OnExitShipComputer()
    {
        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.RemoveScreenPrompt(_modeSwapPrompt);
        promptManager.RemoveScreenPrompt(_modeSelectorPrompt);
    }
}