using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;

namespace CustomShipLogModes;

public class CustomShipLogModes : ModBehaviour
{
    public static CustomShipLogModes Instance;

    private ModSelectorMode _modSelectorMode;
    private Dictionary<ShipLogMode, Tuple<Func<bool>, Func<string>>> _modes = new();

    private ShipLogController _shipLogController;
    private ShipLogMode _requestedChaneMode;

    private ScreenPrompt _modeSelectorPrompt;
    private ScreenPrompt _modeSwapPrompt;

    private bool _AModeAvailable;
    private bool _BModeAvailable;

    private void Start()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Instance = this;
        LoadManager.OnStartSceneLoad += OnStartSceneLoad;
    }

    public override object GetApi() {
        return new CustomShipLogModesAPI();
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

        if (_shipLogController != null)
        {
            ModHelper.Console.WriteLine("The ShipLogController is NOT null, something is wrong!", MessageType.Error);
        }
        _shipLogController = FindObjectOfType<ShipLogController>();

        // Initialize all already added modes, even disabled ones
        foreach (ShipLogMode mode in _modes.Keys)
        {
            if (mode != null)
            {
                InitializeMode(mode);
            }
        }
        
        // Create mod selector mode
        GameObject mapModeGo = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode");
        GameObject selectorModeGo = Instantiate(mapModeGo, mapModeGo.transform.position, mapModeGo.transform.rotation, mapModeGo.transform.parent);
        selectorModeGo.name = nameof(ModSelectorMode);
        _shipLogController._upperRightPromptList.transform.parent.SetAsLastSibling(); // We want to see the prompts on top of the mode selector!
        _modSelectorMode = selectorModeGo.AddComponent<ModSelectorMode>();
        InitializeMode(_modSelectorMode); // We don't add this mode to _modes, so initialize it here

        ShipLogMode a  = selectorModeGo.AddComponent<ModeA>();
        ShipLogMode b  = selectorModeGo.AddComponent<ModeB>();
        AddMode(b, () => _AModeAvailable, () => "Mode B");
        AddMode(a, () => _BModeAvailable, () => "Mode A");
    }

    private void SetupPrompts()
    {
        _modeSelectorPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.OpenModeSelector), ModSelectorMode.Name);
        _modeSwapPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.SwapMode), ""); // The text is updated
    }

    private void InitializeMode(ShipLogMode mode)
    {
        bool canvasActive = _shipLogController._shipLogCanvas.gameObject.activeSelf;
        _shipLogController._shipLogCanvas.gameObject.SetActive(true);
        mode.Initialize(_shipLogController._centerPromptList, _shipLogController._upperRightPromptList, _shipLogController._oneShotSource);
        _shipLogController._shipLogCanvas.gameObject.SetActive(canvasActive);
    }

    // API method
    public void AddMode(ShipLogMode mode, Func<bool> isEnabledSupplier, Func<string> nameSupplier)
    {
        if (_modes.ContainsKey(mode))
        {
            ModHelper.Console.WriteLine("Mode " + mode + " already added, replacing suppliers...", MessageType.Info);
        }
        _modes[mode] = new Tuple<Func<bool>, Func<string>>(isEnabledSupplier, nameSupplier);

        if (_shipLogController != null)
        {
            // Vanilla modes are already initialized in ShipLogController.LateInitialize
            InitializeMode(mode);
        }
    }

    public void RequestChangeMode(ShipLogMode mode)
    {
        // We don't want modes to directly change the mode because that could cause
        // for example closing ana reopening the selector in the same frame because postfix
        // TODO: Add this to API?
        if (_requestedChaneMode == null)
        {
            _requestedChaneMode = mode;
        } 
    }

    public void UpdatePromptsVisibility()
    {
        if (OWInput.IsNewlyPressed(InputLibrary.autopilot))
        {
            _AModeAvailable = !_AModeAvailable;
        }
        if (OWInput.IsNewlyPressed(InputLibrary.markEntryOnHUD))
        {
            _BModeAvailable = !_BModeAvailable;
        }
        
        
        ShipLogMode currentMode = _shipLogController._currentMode;
        List<ShipLogMode> customModes = GetCustomModes();
        bool isCustomMode = customModes.Contains(currentMode);
        List<Tuple<ShipLogMode,string>> availableNamedModes = GetAvailableNamedModes();

        // TODO: weird position (Back to Map Mode -> Select Mode)
        // If Map Mode doesn't allow swap because no Rumor Mode is disabled, we still want to be able to open custom modes...
        bool swapAllowed = currentMode.AllowModeSwap() || currentMode == _shipLogController._mapMode;
        bool enoughModes = customModes.Count >= 1;
        _modeSelectorPrompt.SetVisibility(swapAllowed && enoughModes);

        _modeSwapPrompt.SetVisibility(isCustomMode && currentMode.AllowModeSwap()); // Vanilla modes add their own prompts
        _modeSwapPrompt.SetText(availableNamedModes.Find(m => m.Item1 == GetDefaultMode()).Item2);
    }

    internal void UpdateChangeMode()
    {
        if (_requestedChaneMode != null)
        {
            ChangeMode(_requestedChaneMode);
            _requestedChaneMode = null;
            return;
        }
        
        // TODO: Use same for UpdatePromptsVisibility and UpdateChangeMode?
        ShipLogMode currentMode = _shipLogController._currentMode;
        List<ShipLogMode> customModes = GetCustomModes();
        bool isCustomMode = customModes.Contains(currentMode);
        
        if (_modeSelectorPrompt._isVisible && Input.IsNewlyPressed(Input.Action.OpenModeSelector))
        {
            // We know AllowModeSwap is true (and other necessary conditions because of UpdatePromptsVisibility),
            // except in Map Mode case (see special case)
            _modSelectorMode.SetGoBackMode(currentMode);
            ChangeMode(_modSelectorMode);
            return;
        }
        if (currentMode.AllowModeSwap() && Input.IsNewlyPressed(Input.Action.SwapMode))
        {
            // Don't check _modeSwapPrompt._isVisible (because of vanilla cases)
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
                ChangeMode(GetDefaultMode());
                return;   
            }
        }

        if (_modes.ContainsKey(currentMode) && !customModes.Contains(currentMode))
        {
            // Just in case someone disabled the current custom mode, trapping us there!
            ChangeMode(GetDefaultMode());
        }
    }

    private ShipLogMode GetDefaultMode()
    {
        return PlayerData.GetDetectiveModeEnabled() ? _shipLogController._detectiveMode : _shipLogController._mapMode;
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
        // TODO: Remove?
        return mode == _shipLogController._mapMode || mode == _shipLogController._detectiveMode;
    }
    
    public List<Tuple<ShipLogMode, string>> GetAvailableNamedModes()
    {
        // TODO: Cache per update?
        List<Tuple<ShipLogMode, string>> modes = GetCustomModes()
            .Select(mode => new Tuple<ShipLogMode, string>(mode, _modes[mode].Item2.Invoke()))
            .OrderBy(mode => mode.Item2)
            .ToList();

        // Add vanilla modes
        if (PlayerData.GetDetectiveModeEnabled())
        {
            modes.Add(new Tuple<ShipLogMode, string>(_shipLogController._detectiveMode, UITextLibrary.GetString(UITextType.LogRumorModePrompt)));
        }
        modes.Add(new Tuple<ShipLogMode, string>(_shipLogController._mapMode, UITextLibrary.GetString(UITextType.LogMapModePrompt)));

        return modes;
    }

    private List<ShipLogMode> GetCustomModes()
    {
        // TODO: Cache per update?
        List<ShipLogMode> customModes = new List<ShipLogMode>();
        foreach (var (mode, tuple) in _modes)
        {
            if (mode != null && tuple.Item1.Invoke())
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
        // TODO: Review no detective enabled, it always defaults to map mode instead of last mode, probably nobody cares
        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.AddScreenPrompt(_modeSelectorPrompt, _shipLogController._upperRightPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_modeSwapPrompt, _shipLogController._upperRightPromptList, TextAnchor.MiddleRight);
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