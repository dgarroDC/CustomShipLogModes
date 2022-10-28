using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CustomShipLogModes;

// TODO: Mode, not Mod
public class ModSelectorMode : ItemListMode
{
    // TODO: Translation
    public const string Name = "Select Mode";

    protected List<Tuple<ShipLogMode,string>> _modes = new();
    
    private ScreenPrompt _closePrompt;
    private ScreenPrompt _selectPrompt;
    
    private string _prevEntryId;
    private ShipLogMode _goBackMode;

    public override string GetModeName()
    {
        return Name;
    }

    private void UpdateAvailableModes()
    {
        List<Tuple<ShipLogMode,string>> modes = CustomShipLogModes.Instance.GetAvailableNamedModes();
        if (!modes.SequenceEqual(_modes))
        {
            _modes = modes;
            UpdateItemCount(_modes.Count);
            for (var i = 0; i < _modes.Count; i++)
            {
                ListItems[i]._nameField.text = _modes[i].Item2;
            }
        }
    }

    private string GetModeName(int i)
    {
        return _modes[i].Item2;
    }

    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        base.Initialize(centerPromptList, upperRightPromptList, oneShotSource);

        SetupPrompts();
    }

    private void SetupPrompts()
    {
        // The text is updated
        _closePrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.CloseModeSelector), "");
        _selectPrompt = new ScreenPrompt(Input.PromptCommands(Input.Action.SelectMode), "");
    }

    private void UpdatePromptsVisibility()
    {
        // TODO: Translations
        int goBackFind = _modes.FindIndex(m => m.Item1 == _goBackMode);
        bool canGoBack = goBackFind != -1;
        _closePrompt.SetVisibility(canGoBack);
        if (canGoBack)
        {
            _closePrompt.SetText("Go Back To " + GetModeName(goBackFind));
        }

        _selectPrompt.SetVisibility(true); // This is always possible I guess?
        _selectPrompt.SetText("Select " + GetModeName(SelectedIndex));
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        base.EnterMode(entryID, revealQueue);

        // Yes, I'm using this sound for this, but it actually sounds similar to the vanilla modes enter sounds
        OneShotSource.PlayOneShot(AudioType.Ghost_Laugh);
        _prevEntryId = entryID;

        UpdateAvailableModes();

        UpdatePromptsVisibility(); // Just in case?

        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.AddScreenPrompt(_closePrompt, UpperRightPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_selectPrompt, UpperRightPromptList, TextAnchor.MiddleRight);
    }

    public override void ExitMode()
    {
        base.ExitMode();

        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.RemoveScreenPrompt(_closePrompt);
        promptManager.RemoveScreenPrompt(_selectPrompt);
    }

    public override void UpdateMode()
    {
        base.UpdateMode();
        
        // Just in case a mode was disabled/added/renamed, do we really need to check this now?
        UpdateAvailableModes();

        UpdatePromptsVisibility();
        if (_closePrompt._isVisible && Input.IsNewlyPressed(Input.Action.CloseModeSelector))
        {
            CustomShipLogModes.Instance.RequestChangeMode(_goBackMode); // It could be inactive but ok
            return;
        }
        if (Input.IsNewlyPressed(Input.Action.SelectMode))
        {
            CustomShipLogModes.Instance.RequestChangeMode(_modes[SelectedIndex].Item1);
        }
    }

    public void SetGoBackMode(ShipLogMode mode)
    {
        _goBackMode = mode;
    }

    public override string GetFocusedEntryID()
    {
        return _prevEntryId;
    }

    public override bool AllowCancelInput()
    {
        // We use the "go back" (close mod selector) instead
        return false;
    }

    public override bool AllowModeSwap()
    {
        // You can only "go back" or select a mode
        return false;
    }
}