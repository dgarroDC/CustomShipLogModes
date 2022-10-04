using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CustomShipLogModes;

// TODO: Extract TextListMode
// Heavily based on ShipLogMapMode
public class ModSelectorMode : ShipLogMode
{
    // TODO: Translation
    public const string Name = "Select Mode";

    private ScreenPromptList _upperRightPromptList;
    private OWAudioSource _oneShotSource;
    private CanvasGroupAnimator _mapModeAnimator;
    private RectTransform _entryListRoot;
    private Vector2 _origEntryListPos;
    private List<ShipLogEntryListItem> _listItems;
    private int _entryIndex;
    private ListNavigator _listNavigator;
    private RectTransform _entrySelectArrow;

    private ScreenPrompt _closePrompt;
    private ScreenPrompt _selectPrompt;
    
    private string _prevEntryId;
    private List<Tuple<ShipLogMode,string>> _modes = new();
    private ShipLogMode _goBackMode;

    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        _upperRightPromptList = upperRightPromptList;
        _oneShotSource = oneShotSource;

        SetupPrompts();

        // Modify map mode clone
        Destroy(gameObject.transform.Find("ScaleRoot").gameObject);
        Destroy(gameObject.transform.Find("ReticleImage").gameObject);
        gameObject.DestroyAllComponents<ShipLogMapMode>();

        RectTransform entryMenu = gameObject.transform.Find("EntryMenu").GetRequiredComponent<RectTransform>();

        // Init animations
        _mapModeAnimator = gameObject.GetComponent<CanvasGroupAnimator>();
        // Change animation?
        _mapModeAnimator.SetImmediate(0f, Vector3.one * 0.5f);
        entryMenu.GetComponent<CanvasGroupAnimator>().SetImmediate(1f, Vector3.one); // Always visible inside the mode

        // Delete photo and expand entry list horizontally, maybe in the future we could use photos 
        Transform photoRoot = entryMenu.transform.Find("PhotoRoot");
        Destroy(photoRoot.gameObject);
        // idk this seems to work
        RectTransform entryListRoot = entryMenu.transform.Find("EntryListRoot").GetComponent<RectTransform>();
        entryListRoot.anchorMax = new Vector2(1, 1);
        entryListRoot.offsetMax = new Vector2(0, 0);

        // Expand vertically because we don't currently use description field
        // Magic number to match the bottom line with the description field, idk how to properly calculate it
        entryMenu.offsetMin = new Vector2(entryMenu.offsetMin.x, -594); 

        gameObject.transform.Find("NamePanelRoot").Find("Name").GetComponent<Text>().text = Name;

        // Init entry list
        _entryListRoot = entryListRoot.Find("EntryList").GetRequiredComponent<RectTransform>();
        _origEntryListPos = _entryListRoot.anchoredPosition;
        // TODO: Mimic delay (????)
        ShipLogEntryListItem[] oldListItems = _entryListRoot.GetComponentsInChildren<ShipLogEntryListItem>();
        int keepEntries = 1;
        for (int i = keepEntries; i < oldListItems.Length; i++)
        {
            Destroy(oldListItems[i].gameObject);
        }
        _listItems = new List<ShipLogEntryListItem>();
        // We now Init called in this one and so all copies will be TODO: Review in TextListMode?
        SetupAndAddItem(oldListItems[0]);
        _entrySelectArrow = _entryListRoot.transform.Find("SelectArrow").GetRequiredComponent<RectTransform>();
        _listNavigator = new ListNavigator();
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
        Tuple<ShipLogMode,string> goBackFind = _modes.Find(m => m.Item1 == _goBackMode);
        bool canGoBack = goBackFind != default;
        _closePrompt.SetVisibility(canGoBack);
        if (canGoBack)
        {
            _closePrompt.SetText("Go Back To " + goBackFind.Item2);
        }
        
        _selectPrompt.SetVisibility(true); // This is always possible I guess?
        _selectPrompt.SetText("Select " + _modes[_entryIndex].Item2);
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        // Yes, I'm using this sound for this, but it actually sounds similar to the vanilla modes enter sounds
        _oneShotSource.PlayOneShot(AudioType.Ghost_Laugh);
        _mapModeAnimator.AnimateTo(1f, Vector3.one, 0.5f);
        _prevEntryId = entryID;
        
        UpdateAvailableModes();
        UpdatePromptsVisibility(); // Just in case?
        UpdateListItemVisuals(); // Do this because otherwise alphas are reset or something

        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.AddScreenPrompt(_closePrompt, _upperRightPromptList, TextAnchor.MiddleRight);
        promptManager.AddScreenPrompt(_selectPrompt, _upperRightPromptList, TextAnchor.MiddleRight);
    }

    public override void ExitMode()
    {
        _mapModeAnimator.AnimateTo(0f, Vector3.one * 0.5f, 0.5f);

        PromptManager promptManager = Locator.GetPromptManager();
        promptManager.RemoveScreenPrompt(_closePrompt);
        promptManager.RemoveScreenPrompt(_selectPrompt);
    }

    public void AddEntry()
    {
        GameObject template = _listItems[0].gameObject;
        GameObject newEntry = Instantiate(template, template.transform.parent);
        newEntry.name = "EntryListItem_" + _listItems.Count;
        ShipLogEntryListItem item = newEntry.GetComponent<ShipLogEntryListItem>();
        SetupAndAddItem(item);
    }

    private void SetEntryFocus(int index)
    {
        if (index == -1)
        {
            index = _modes.Count - 1; // Important to use the mod list here, not the entry list!!!
        }
        else if (index == _modes.Count)
        {
            index = 0;
        }

        int topIndex = Mathf.Max(0, index - 4);
        // We need at least 2 items, but this is always the case because even if we could have only one mode (map mode),
        // to open the menu at least two custom modes were available, so 3 mods in total and that means at least 2 items
        // because we don't delete them TODO: Review precondition no longer valid in an arbitrary TextListMode if created
        float itemsSpace = _listItems[1].gameObject.GetComponent<RectTransform>().anchoredPosition.y -
                           _listItems[0].gameObject.GetComponent<RectTransform>().anchoredPosition.y; 
        _entryListRoot.anchoredPosition = _origEntryListPos - new Vector2(0f, topIndex * itemsSpace);

        Vector3 origArrowPos = _entrySelectArrow.localPosition;
        Vector3 targetArrowY = _entrySelectArrow.parent.InverseTransformPoint(this._listItems[index].GetSelectionArrowPosition());
        _entrySelectArrow.localPosition = new Vector3(origArrowPos.x, targetArrowY.y, origArrowPos.z);

        _entryIndex = index;
        UpdateListItemVisuals();
    }

    private void UpdateListItemVisuals()
    {
        for (int i = 0; i < _listItems.Count; i++)
        {
            bool focus = i == _entryIndex;
            SetFocus(_listItems[i], focus);
            int topIndex = Mathf.Max(0, _entryIndex - 4);
            int lastOpaqueIndex = 4 + topIndex;
            // Don't use vanilla (only + 2) since we have a loot of room because no description field
            // (although we won't probably have so many modes, we would need a search at that point lol)
            int lastVisibleIndex = lastOpaqueIndex + 9;
            if (i < topIndex)
            {
                _listItems[i].SetListAlpha(0f);
            }
            else if (i <= lastOpaqueIndex - 1)
            {
                _listItems[i].SetListAlpha(1f);
            }
            else if (i == lastOpaqueIndex)
            {
                _listItems[i].SetListAlpha(0.5f);
            }
            else if (i == lastOpaqueIndex + 1)
            {
                _listItems[i].SetListAlpha(0.2f);
            }
            else if (i <= lastVisibleIndex)
            {
                _listItems[i].SetListAlpha(0.05f);
            }
            else
            {
                _listItems[i].SetListAlpha(0f);
            }
        }
    }

    private void SetFocus(ShipLogEntryListItem item, bool focus)
    {
        item._focusAlpha = focus ? 1f : 0.2f;
        item.UpdateAlpha();
    }
    
    private void SetupAndAddItem(ShipLogEntryListItem item)
    {
        item._unreadIcon.gameObject.SetActive(false);
        item._hudMarkerIcon.gameObject.SetActive(false);
        item._moreToExploreIcon.gameObject.SetActive(false);
        // This is probably false already, we don't want to call Update() (no animation or entry)
        item.enabled = false;
        _listItems.Add(item);
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
        // Just in case a mode was disabled/added/renamed, do we really need to check this now?
        UpdateAvailableModes();
        
        UpdatePromptsVisibility();
        if (_closePrompt._isVisible && Input.IsNewlyPressed(Input.Action.CloseModeSelector))
        {
            // Check null just in case this mode wasn't opened from the expected path
            CustomShipLogModes.Instance.RequestChangeMode(_goBackMode); // It could be inactive but ok
            return;
        }
        if (Input.IsNewlyPressed(Input.Action.SelectMode))
        {
            CustomShipLogModes.Instance.RequestChangeMode(_modes[_entryIndex].Item1);
            return;
        }
        
        int selectionChange = _listNavigator.GetSelectionChange();
        if (selectionChange != 0)
        {
            SetEntryFocus(_entryIndex + selectionChange);
            // Don't play sound in SetEntryFocus to avoid playing it in UpdateAvailableModes (particularly on first time of EnterMode)
            _oneShotSource.PlayOneShot(AudioType.ShipLogMoveBetweenEntries);
        }
    }

    public void SetGoBackMode(ShipLogMode mode)
    {
        _goBackMode = mode;
    }

    private void UpdateAvailableModes()
    {
        List<Tuple<ShipLogMode, string>> modes = CustomShipLogModes.Instance.GetAvailableNamedModes();
        if (!modes.SequenceEqual(_modes))
        {
            _modes = modes;
            while (_listItems.Count < modes.Count)
            {
                AddEntry();
            }
            for (var i = 0; i < _listItems.Count; i++)
            {
                ShipLogEntryListItem item = _listItems[i];
                if (i < modes.Count)
                {
                    item.gameObject.SetActive(true);
                    item._nameField.text = modes[i].Item2;
                }
                else
                {
                    item.gameObject.SetActive(false);
                }
            }
            
            SetEntryFocus(0); // TODO: Try to select the previous selection?
        }
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