using System;
using System.Collections.Generic;
using System.Linq;
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

    private ShipLogController _shipLogController;
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
        LoadManager.OnStartSceneLoad += OnStartSceneLoad;
    }

    private void OnStartSceneLoad(OWScene originalScene, OWScene loadScene)
    {
        // Do it on start and not on completed to prevent mods adding modes before resetting the _shipLogController
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

        AddMode(shipLogController._detectiveMode, () => PlayerData.GetDetectiveModeEnabled(), () => UITextLibrary.GetString(UITextType.LogRumorModePrompt));
        AddMode(shipLogController._mapMode, () => true, () => UITextLibrary.GetString(UITextType.LogMapModePrompt));

        ShipLogMode a  = selectorModeGo.AddComponent<ModeA>();
        ShipLogMode b  = selectorModeGo.AddComponent<ModeB>();
        InitializeMode(a);
        InitializeMode(b);
        AddMode(b, () => true, () => "Mode B");
        AddMode(a, () => true, () => "Mode A");
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

        // TODO: weird position (Back to Map Mode -> Select Mode)
        // If Map Mode doesn't allow swap because no Rumor Mode is disabled, we still want to be able to open custom modes...
        bool swapAllowed = currentMode.AllowModeSwap() || currentMode == _shipLogController._mapMode;
        bool enoughModes = (IsVanillaMode(currentMode) && customModes.Count >= 1) 
                           || (isCustomMode && customModes.Count >= 2);
        _modeSelectorPrompt.SetVisibility(swapAllowed && enoughModes);
        _modeSelectorPrompt.SetText(customModes.Count == 1? GetModeName(customModes[0]) : "Select Mode");

        _modeSwapPrompt.SetVisibility(isCustomMode && currentMode.AllowModeSwap()); // Vanilla modes add their own prompts
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
        
        if (_modeSelectorPrompt.IsVisible() && Input.IsNewlyPressed(Input.Action.OpenModeSelector))
        {
            // We know AllowModeSwap is true (and other necessary conditions because of UpdatePromptsVisibility),
            // except in Map Mode case (see special case)
            if (customModes.Count == 1)
            {
                ChangeMode(customModes[0]);
                return;
            }
            _goBackMode = currentMode;
            ChangeMode(_modSelectorMode);
            return;
        }
        if (currentMode.AllowModeSwap() && Input.IsNewlyPressed(Input.Action.SwapMode))
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

        if (currentMode == _modSelectorMode && Input.IsNewlyPressed(Input.Action.CloseModeSelector) && _goBackMode != null)
        {
            // Check null just in case this mode wasn't opened from the expected path
            // I would like to move this to ModSelectorMode.UpdateMode, but UpdateMode is called before the postfix and could reopen the menu 
            // TODO: Don't show the prompt in that case
            ChangeMode(_goBackMode); // It could be inactive but ok
        }
        if (currentMode == _modSelectorMode && Input.IsNewlyPressed(Input.Action.SelectMode))
        {
            ChangeMode(_modSelectorMode.GetSelectedMode());
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
    
    public List<Tuple<ShipLogMode, string>> GetAvailableNamedModes()
    {
        // TODO: Cache per update?
        List<Tuple<ShipLogMode, string>> modes = new();
        foreach (var (mode, tuple) in _modes)
        {
            if (mode != null && tuple.Item1.Invoke())
            {
                modes.Add(new Tuple<ShipLogMode, string>(mode, tuple.Item2.Invoke()));
            }
        }

        return modes
            .OrderBy(mode => IsVanillaMode(mode.Item1))
            .ThenBy(mode => mode.Item2)
            .ToList();
    }

    private List<ShipLogMode> GetCustomModes()
    {
        // TODO: Cache per update?
        return GetAvailableNamedModes().Select(t => t.Item1).Where(mode => !IsVanillaMode(mode)).ToList();
    }

    private string GetModeName(ShipLogMode mode)
    {
        return _modes[mode].Item2.Invoke();
    }

    public void OnEnterShipComputer()
    {
        // TODO: MAP MODE CASE, ON ENTER COMPUTER DEFAULTS TO IT
        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.AddScreenPrompt(_modeSwapPrompt, _shipLogController._upperRightPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_modeSelectorPrompt, _shipLogController._upperRightPromptList, TextAnchor.MiddleRight);
        foreach (ShipLogMode mode in GetCustomModes())
        {
            mode.OnEnterComputer();
        }
    }

    public void OnExitShipComputer()
    {
        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.RemoveScreenPrompt(_modeSwapPrompt);
        promptManager.RemoveScreenPrompt(_modeSelectorPrompt);
        foreach (ShipLogMode mode in GetCustomModes())
        {
            mode.OnExitComputer();
        }
    }
}