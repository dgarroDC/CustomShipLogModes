using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CustomShipLogModes;

// Heavily based on ShipLogMapMode
public abstract class ItemListMode : ShipLogMode
{
    protected ScreenPromptList CenterPromptList;
    protected ScreenPromptList UpperRightPromptList;
    protected OWAudioSource OneShotSource;

    protected int SelectedIndex;
    protected List<ShipLogEntryListItem> ListItems;

    private CanvasGroupAnimator _mapModeAnimator;
    private RectTransform _entryListRoot;
    private Vector2 _origEntryListPos;
    private int _itemCount = -1;
    private ListNavigator _listNavigator;
    private RectTransform _entrySelectArrow;
    private FontAndLanguageController _fontAndLanguageController; // Do we really need this?

    public static T Make<T>() where T : ItemListMode
    {
        GameObject mapModeGo = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode");
        GameObject itemListModeGo = Instantiate(mapModeGo, mapModeGo.transform.position, mapModeGo.transform.rotation, mapModeGo.transform.parent);
        T itemListMode = itemListModeGo.AddComponent<T>();
        itemListModeGo.name = typeof(T).Name;
        return itemListMode;
    }

    public abstract string GetModeName();

    protected virtual void OnItemSelected()
    {
        // No-op
        // TODO: On enter? Index starting in -1?
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
        _fontAndLanguageController = gameObject.GetComponent<ShipLogMapMode>()._fontAndLanguageController;

        RectTransform entryMenu = gameObject.transform.Find("EntryMenu").GetRequiredComponent<RectTransform>();
        // Init animations
        _mapModeAnimator = gameObject.GetComponent<CanvasGroupAnimator>();
        // Change animation?
        _mapModeAnimator.SetImmediate(0f, Vector3.one * 0.5f);
        entryMenu.GetComponent<CanvasGroupAnimator>().SetImmediate(1f, Vector3.one); // Always visible inside the mode

        // Delete photo and expand entry list horizontally, maybe in the future we could use photos 
        // TODO: Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode/EntryMenu/PhotoRoot/MarkHUDRoot/
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
        ShipLogEntryListItem[] oldListItems = _entryListRoot.GetComponentsInChildren<ShipLogEntryListItem>();
        int keepEntries = 1;
        for (int i = keepEntries; i < oldListItems.Length; i++)
        {
            Destroy(oldListItems[i].gameObject);
        }
        ListItems = new List<ShipLogEntryListItem>();
        SetupAndAddItem(oldListItems[0]);
        _entrySelectArrow = _entryListRoot.transform.Find("SelectArrow").GetRequiredComponent<RectTransform>();
        _listNavigator = new ListNavigator();

        _itemCount = -1; // To force changing UI stuff
        UpdateItemCount(0); // We need in case there aren't items yet (EnterMode in subclass won't add them), hide template entry and arrow
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        _mapModeAnimator.AnimateTo(1f, Vector3.one, 0.5f);
    }

    public override void ExitMode()
    {
        _mapModeAnimator.AnimateTo(0f, Vector3.one * 0.5f, 0.5f);
    }

    public void AddEntry()
    {
        GameObject template = ListItems[0].gameObject;
        GameObject newEntry = Instantiate(template, template.transform.parent);
        newEntry.name = "EntryListItem_" + ListItems.Count;
        ShipLogEntryListItem item = newEntry.GetComponent<ShipLogEntryListItem>();
        SetupAndAddItem(item);
    }

    protected void SetEntryFocus(int index)
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
            // TODO: Remove this case, just create some items on setup..
            _entryListRoot.anchoredPosition = _origEntryListPos;
        }
        else
        {
            // There are at least two items, so there are at least two UI items
            float itemsSpace = ListItems[1].gameObject.GetComponent<RectTransform>().anchoredPosition.y -
                               ListItems[0].gameObject.GetComponent<RectTransform>().anchoredPosition.y;    
            _entryListRoot.anchoredPosition = _origEntryListPos - new Vector2(0f, topIndex * itemsSpace);
        }

        Vector3 origArrowPos = _entrySelectArrow.localPosition;
        Vector3 targetArrowY = _entrySelectArrow.parent.InverseTransformPoint(this.ListItems[index].GetSelectionArrowPosition());
        _entrySelectArrow.localPosition = new Vector3(origArrowPos.x, targetArrowY.y, origArrowPos.z);

        SelectedIndex = index;
        UpdateListItemAlphas();

        OnItemSelected();
    }

    private void UpdateListItemAlphas()
    {
        // AKA UpdateListItemVisuals
        for (int i = 0; i < ListItems.Count; i++)
        {
            bool focus = i == SelectedIndex;
            SetFocus(ListItems[i], focus);
            int topIndex = Mathf.Max(0, SelectedIndex - 4);
            int lastOpaqueIndex = 4 + topIndex;
            // Don't use vanilla (only + 2) since we have a loot of room because no description field
            // TODO: Possible desc field
            int lastVisibleIndex = lastOpaqueIndex + 9;
            if (i < topIndex)
            {
                ListItems[i].SetListAlpha(0f);
            }
            else if (i <= lastOpaqueIndex - 1)
            {
                ListItems[i].SetListAlpha(1f);
            }
            else if (i == lastOpaqueIndex)
            {
                ListItems[i].SetListAlpha(0.5f);
            }
            else if (i == lastOpaqueIndex + 1)
            {
                ListItems[i].SetListAlpha(0.2f);
            }
            else if (i <= lastVisibleIndex)
            {
                ListItems[i].SetListAlpha(0.05f);
            }
            else
            {
                ListItems[i].SetListAlpha(0f);
            }
        }
    }

    private void SetFocus(ShipLogEntryListItem item, bool focus)
    {
        // Don't use the item SetFocus, requires entry != null
        if (item._hasFocus != focus)
        {
            // The _hasFocus is to avoid changing the alpha in unnecessary cases maybe...
            item._hasFocus = focus;
            item._focusAlpha = focus ? 1f : 0.2f;
            item.UpdateAlpha();
        }
    }
    
    private void SetupAndAddItem(ShipLogEntryListItem item)
    {
        item.Init(_fontAndLanguageController);
        item._animAlpha = 1f;
        item._focusAlpha = 0.2f; // probably unnecessary because SetFocus, but Setup does it, so just in case...
        item._nameField.text = "If you're reading this, this is a bug, please report it!";
        item._unreadIcon.gameObject.SetActive(false); // Icons also unnecessary? (virtual methods) TODO
        item._hudMarkerIcon.gameObject.SetActive(false);
        item._moreToExploreIcon.gameObject.SetActive(false);
        item.enabled = false;
        // TODO: Add option to AnimateTo? _entry required in Update()!!
        ListItems.Add(item);
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
        if (_itemCount < 2) return;
        int selectionChange = _listNavigator.GetSelectionChange();
        if (selectionChange != 0)
        {
            SetEntryFocus(SelectedIndex + selectionChange);
            // Don't play sound in SetEntryFocus to avoid playing it in some situations
            OneShotSource.PlayOneShot(AudioType.ShipLogMoveBetweenEntries);
        }
    }

    protected void UpdateItemCount(int itemCount)
    {
        if (itemCount == _itemCount) return;
        _itemCount = itemCount;
        while (ListItems.Count < itemCount)
        {
            AddEntry();
        }
        for (var i = 0; i < ListItems.Count; i++)
        {
            ShipLogEntryListItem item = ListItems[i];
            if (i < itemCount)
            {
                item.gameObject.SetActive(true);
            }
            else
            {
                item.gameObject.SetActive(false);
            }
        }
        
        _entrySelectArrow.gameObject.SetActive(itemCount > 0);

        if (SelectedIndex >= itemCount && itemCount > 0)
        { 
            // TODO: Try to select the previous selection?
            SetEntryFocus(_itemCount - 1);
        }
        else
        {
            // This for the case that entries were added on EnterMode for example
            UpdateListItemAlphas();
        }
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
