using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

namespace CustomShipLogModes;

// Heavily based on ShipLogMapMode
public class ModSelectorMode : ShipLogMode
{
    private ScreenPromptList _upperRightPromptList; // TODO: Enter selected mode? (E?)
    private OWAudioSource _oneShotSource;
    private CanvasGroupAnimator _mapModeAnimator;
    private RectTransform _entryListRoot;
    private Vector2 _origEntryListPos;
    private List<ShipLogEntryListItem> _listItems;
    private int _entryIndex;
    private ListNavigator _listNavigator;
    private RectTransform _entrySelectArrow;

    private string _prevEntryId;

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        _oneShotSource.PlayOneShot(AudioType.ShipLogSelectPlanet);
        _mapModeAnimator.AnimateTo(1f, Vector3.one, 0.5f);
        _prevEntryId = entryID;
        // Important if we are the first mode on enter computer (although that shouldn't be possible I think), alpha is reset or something...
        UpdateListItemVisuals();
    }

    public override void ExitMode()
    {
        _mapModeAnimator.AnimateTo(0f, Vector3.one * 0.5f, 0.5f);
        _oneShotSource.PlayOneShot(AudioType.Ghost_Laugh);
    }

    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        _upperRightPromptList = upperRightPromptList;
        _oneShotSource = oneShotSource;

        // Create "fake map mode"
        GameObject mapMode = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode");
        GameObject fakeMapMode = Instantiate(mapMode, mapMode.transform.position, mapMode.transform.rotation, mapMode.transform.parent);
        Destroy(fakeMapMode.transform.Find("ScaleRoot").gameObject);
        Destroy(fakeMapMode.transform.Find("ReticleImage").gameObject);
        fakeMapMode.DestroyAllComponents<ShipLogMapMode>();

        RectTransform entryMenu = fakeMapMode.transform.Find("EntryMenu").GetRequiredComponent<RectTransform>();

        // Init animations
        _mapModeAnimator = fakeMapMode.GetComponent<CanvasGroupAnimator>();
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
        
        // TODO: Translations
        fakeMapMode.transform.Find("NamePanelRoot").Find("Name").GetComponent<Text>().text = "Select Mode";

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
        // TODO: Was Init called in this one? Then all copies too
        _listItems.Add(oldListItems[0]);
        // TODO: Can remove entries?
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        AddEntry();
        _entrySelectArrow = _entryListRoot.transform.Find("SelectArrow").GetRequiredComponent<RectTransform>();
        // Set focus? < 2 entries?
        SetEntryFocus(0);
        
        _listNavigator = new ListNavigator();
    }

    public void AddEntry()
    {
        // Keep same entry focus even if it changes index? (Alphabetical order)
        GameObject template = _listItems[0].gameObject;
        GameObject gameObject = Instantiate(template, template.transform.parent);
        gameObject.name = "EntryListItem_" + _listItems.Count;
        ShipLogEntryListItem item = gameObject.GetComponent<ShipLogEntryListItem>();
        item._nameField.text = "Test " + _listItems.Count;
        _listItems.Add(item);
        //_listItems[i].Init(_fontAndLanguageController);
        UpdateListItemVisuals();
    }

    private void SetEntryFocus(int index)
    {
        if (index == -1)
        {
            index = _listItems.Count - 1;
        }
        else if (index == _listItems.Count)
        {
            index = 0;
        }

        int topIndex = Mathf.Max(0, index - 4);
        // TODO: GetAnchoredPosition, Init not called? Font? I don't remember the issue here. Also Setup?
        float itemsSpace = _listItems[1].gameObject.GetComponent<RectTransform>().anchoredPosition.y -
                           _listItems[0].gameObject.GetComponent<RectTransform>().anchoredPosition.y; // TODO: We need at least 2 items!
        _entryListRoot.anchoredPosition = _origEntryListPos - new Vector2(0f, topIndex * itemsSpace);

        Vector3 origArrowPos = _entrySelectArrow.localPosition;
        Vector3 targetArrowY = _entrySelectArrow.parent.InverseTransformPoint(this._listItems[index].GetSelectionArrowPosition());
        _entrySelectArrow.localPosition = new Vector3(origArrowPos.x, targetArrowY.y, origArrowPos.z);

        _entryIndex = index;
        UpdateListItemVisuals();
        _oneShotSource.PlayOneShot(AudioType.ShipLogMoveBetweenEntries);
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
        item._focusAlpha = (focus ? 1f : 0.2f);
        item.UpdateAlpha();
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
        // TODO: size >= 2?
        int selectionChange = _listNavigator.GetSelectionChange();
        if (selectionChange != 0)
        {
            SetEntryFocus(_entryIndex + selectionChange);
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