using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CustomShipLogModes;

// Heavily based on ShipLogMapMode
// TODO: count == 0?
public abstract class ItemListMode : ShipLogMode
{
    protected ScreenPromptList CenterPromptList;
    protected ScreenPromptList UpperRightPromptList;
    protected OWAudioSource OneShotSource;

    protected int SelectedIndex;

    private CanvasGroupAnimator _mapModeAnimator;
    private RectTransform _entryListRoot;
    private Vector2 _origEntryListPos;
    private int _itemCount;
    private List<ShipLogEntryListItem> _listItems;
    private ListNavigator _listNavigator;
    private RectTransform _entrySelectArrow;

    public static T Make<T>() where T : ItemListMode
    {
        GameObject mapModeGo = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode");
        GameObject itemListModeGo = Instantiate(mapModeGo, mapModeGo.transform.position, mapModeGo.transform.rotation, mapModeGo.transform.parent);
        T itemListMode = itemListModeGo.AddComponent<T>();
        itemListModeGo.name = nameof(T);
        return itemListMode;
    }

    public abstract string GetModeName();

    protected abstract int UpdateAvailableItems();

    protected abstract string GetItemName(int i);

    protected virtual void OnEntrySelected()
    {
        // No-op
    }

    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        CenterPromptList = centerPromptList;
        UpperRightPromptList = upperRightPromptList;
        OneShotSource = oneShotSource;

        UpperRightPromptList.transform.parent.SetAsLastSibling(); // We want to see the prompts on top of the mode!
        
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

        gameObject.transform.Find("NamePanelRoot").Find("Name").GetComponent<Text>().text = GetModeName();

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

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        _mapModeAnimator.AnimateTo(1f, Vector3.one, 0.5f);

        CheckAvailableItems();
        UpdateListItemVisuals(); // Do this because otherwise alphas are reset or something
    }

    public override void ExitMode()
    {
        _mapModeAnimator.AnimateTo(0f, Vector3.one * 0.5f, 0.5f);
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
            index = _itemCount - 1; // Important to use the item list here, not the entry list!!!
        }
        else if (index == _itemCount)
        {
            index = 0;
        }

        int topIndex = Mathf.Max(0, index - 4);
        if (topIndex == 0)
        {
            _entryListRoot.anchoredPosition = _origEntryListPos;
        }
        else
        {
            // There are at least two items, so there are at least two UI items
            float itemsSpace = _listItems[1].gameObject.GetComponent<RectTransform>().anchoredPosition.y -
                               _listItems[0].gameObject.GetComponent<RectTransform>().anchoredPosition.y;    
            _entryListRoot.anchoredPosition = _origEntryListPos - new Vector2(0f, topIndex * itemsSpace);
        }

        Vector3 origArrowPos = _entrySelectArrow.localPosition;
        Vector3 targetArrowY = _entrySelectArrow.parent.InverseTransformPoint(this._listItems[index].GetSelectionArrowPosition());
        _entrySelectArrow.localPosition = new Vector3(origArrowPos.x, targetArrowY.y, origArrowPos.z);

        SelectedIndex = index;
        UpdateListItemVisuals();

        OnEntrySelected();
    }

    private void UpdateListItemVisuals()
    {
        for (int i = 0; i < _listItems.Count; i++)
        {
            bool focus = i == SelectedIndex;
            SetFocus(_listItems[i], focus);
            int topIndex = Mathf.Max(0, SelectedIndex - 4);
            int lastOpaqueIndex = 4 + topIndex;
            // Don't use vanilla (only + 2) since we have a loot of room because no description field
            // TODO: Possible desc field
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
        // Just in case a item was disabled/added/modified
        CheckAvailableItems();

        int selectionChange = _listNavigator.GetSelectionChange();
        if (selectionChange != 0)
        {
            SetEntryFocus(SelectedIndex + selectionChange);
            // Don't play sound in SetEntryFocus to avoid playing it in UpdateAvailableModes (particularly on first time of EnterMode)
            OneShotSource.PlayOneShot(AudioType.ShipLogMoveBetweenEntries);
        }
    }

    private void CheckAvailableItems()
    {
        int itemCount = UpdateAvailableItems();
        if (itemCount == -1) return; // Items not updated
        _itemCount = itemCount;
        while (_listItems.Count < itemCount)
        {
            AddEntry();
        }
        for (var i = 0; i < _listItems.Count; i++)
        {
            ShipLogEntryListItem item = _listItems[i];
            if (i < itemCount)
            {
                item.gameObject.SetActive(true);
                item._nameField.text = GetItemName(i);
            }
            else
            {
                item.gameObject.SetActive(false);
            }
        }

        SetEntryFocus(0); // TODO: Try to select the previous selection?
    }

    public override string GetFocusedEntryID()
    {
        return "";
    }

    public override bool AllowCancelInput()
    {
        return true;
    }

    public override bool AllowModeSwap()
    {
        return true;
    }
}
