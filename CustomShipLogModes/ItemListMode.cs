using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

namespace CustomShipLogModes;

// TODO: Check this._listItems[j].Reset();??? _hasFocus for example
// TODO: ShipLogEntryDescriptionFieldUtils : GetNext, Reset, ???

// Heavily based on ShipLogMapMode
public abstract class ItemListMode : ShipLogMode
{
    protected ScreenPromptList CenterPromptList;
    protected ScreenPromptList UpperRightPromptList;
    protected OWAudioSource OneShotSource;

    protected int SelectedIndex;
    protected List<ShipLogEntryListItem> ListItems;

    protected Image Photo;
    protected Text QuestionMark;
    protected ShipLogEntryDescriptionField DescriptionField;

    private bool _usePhotoAndDescField;
   
    private CanvasGroupAnimator _mapModeAnimator;
    private RectTransform _entryListRoot;
    private Vector2 _origEntryListPos;
    private int _itemCount = -1;
    private ListNavigator _listNavigator;
    private RectTransform _entrySelectArrow;
    private FontAndLanguageController _fontAndLanguageController; // Do we really need this?

    public static T Make<T>(bool usePhotoAndDescField) where T : ItemListMode
    {
        // TODO: Somehow do this after ShipLogMapMode.Initialize? Reuse entry list and GetMapMode() instead of Find...
        GameObject mapModeGo = GameObject.Find("Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode");
        GameObject itemListModeGo = Instantiate(mapModeGo, mapModeGo.transform.position, mapModeGo.transform.rotation, mapModeGo.transform.parent);
        T itemListMode = itemListModeGo.AddComponent<T>();
        itemListMode._usePhotoAndDescField = usePhotoAndDescField;
        itemListModeGo.name = typeof(T).Name;
        return itemListMode;
        // TODO: Fix that if you run this after map mode init then the icons are in wrong place? ALSO ALPHA?
        // TODO: it's ultra BROKEN if Make is run in same frame after Destroy of map mode entry template!!! Solution? Keep only the last entry? But why?
        // TODO: Also broken if map mode had selected entry > n (list scrolled) on copy, _origEntryListPos would be wrong! Copy _origEntryListPos from Map mode?
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

        ShipLogMapMode mapMode = gameObject.GetComponent<ShipLogMapMode>();
        _entryListRoot = mapMode._entryListRoot; // /EntryMenu/EntryListRoot/EntryList

        // Init animations (allow changing?)
        _mapModeAnimator = mapMode._mapModeAnimator;
        _mapModeAnimator.SetImmediate(0f, Vector3.one * 0.5f);
        mapMode._entryMenuAnimator.SetImmediate(1f, Vector3.one); // Always visible inside the mode

        if (!_usePhotoAndDescField)
        {
            // TODO: Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode/EntryMenu/PhotoRoot/MarkHUDRoot/
            // Hide photo (root) and expand entry list horizontally
            Transform photoRoot = mapMode._photoRoot;
            photoRoot.gameObject.SetActive(false);
            // idk this seems to work
            RectTransform entryListRoot = (RectTransform)_entryListRoot.parent;
            entryListRoot.anchorMax = new Vector2(1, 1);
            entryListRoot.offsetMax = new Vector2(0, 0);
            
            // Expand vertically because we don't currently use description field
            // Magic number to match the bottom line with the description field, idk how to properly calculate it
            RectTransform entryMenu = entryListRoot.parent as RectTransform; // Could also get from mapMode._entryMenuAnimator
            entryMenu.offsetMin = new Vector2(entryMenu.offsetMin.x, -594);
        }
        else
        {
            // Photo & Question Mark
            Photo = mapMode._photo;
            // Don't start with Map Mode's last viewed image (although the implementer could just cover this...)
            Texture2D texture = Texture2D.blackTexture;
            Photo.sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            Photo.gameObject.SetActive(false);
            QuestionMark = mapMode._questionMark;
            QuestionMark.gameObject.SetActive(false);

            // Description Field
            GameObject descriptionFieldGo = mapMode._descriptionField.gameObject;
            GameObject descriptionFieldClone = Instantiate(descriptionFieldGo, descriptionFieldGo.transform.position, descriptionFieldGo.transform.rotation, transform);
            descriptionFieldClone.transform.localPosition = descriptionFieldGo.transform.localPosition;
            DescriptionField = descriptionFieldClone.GetComponent<ShipLogEntryDescriptionField>();
            DescriptionField._animator.SetImmediate(1f, Vector3.one); // Always visible inside the mode
            DescriptionField.enabled = true;
            // Recover null fields?
            CustomShipLogModes.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() => // TODO NULLS!
                DescriptionField._factListItems = DescriptionField.GetComponentsInChildren<ShipLogFactListItem>(true));
            // for (int i = 1; i < DescriptionField._factListItems.Length; i++)
            // {
            //     DescriptionField._factListItems[i] = factItems[i];
            // }
            DescriptionField._scrollPromptGamepad = new ScreenPrompt(InputLibrary.scrollLogText, UITextLibrary.GetString(UITextType.LogScrollTextPrompt));
            DescriptionField._scrollPromptKbm = new ScreenPrompt(InputLibrary.toolOptionY, UITextLibrary.GetString(UITextType.LogScrollTextPrompt));
        }

        _fontAndLanguageController = mapMode._fontAndLanguageController;
        // nameField.font = Locator.GetUIStyleManager().GetShipLogFont(); // TODO: Probably not needed, but ShipLogMapMode does it, but it looks off...
        mapMode._nameField.text = GetModeName(); // NamePanelRoot/Name
        // TODO: Update on Enter? Or on update, so the subclass can change it? Maybe protected field? 

        // Init entry list
        ShipLogEntryListItem[] oldListItems = _entryListRoot.GetComponentsInChildren<ShipLogEntryListItem>(true);
        // TODO: Analyze SuitLog approach: Limited entries that don't move (potential compatibility break!)
        // TODO: Explain why we keep last!!!
        for (int i = 0; i < oldListItems.Length - 1; i++)
        {
            Destroy(oldListItems[i].gameObject);
        }
        ListItems = new List<ShipLogEntryListItem>();
        SetupAndAddItem(oldListItems[oldListItems.Length - 1]);
        _entrySelectArrow = mapMode._entrySelectArrow;
        _listNavigator = new ListNavigator();
        
        // Map mode was already initialized, maybe even before Make, the entry list post may be scrolled, use its original position
        _origEntryListPos = CustomShipLogModes.Instance.GetMapMode()._origEntryListPos;

        _itemCount = -1; // To force changing UI stuff
        UpdateItemCount(0); // We need in case there aren't items yet (EnterMode in subclass won't add them), hide kept entry and arrow
        
        // Hide/Destroy Map Mode specific stuff
        mapMode._scaleRoot.gameObject.SetActive(false);
        mapMode._reticleAnimator.gameObject.SetActive(false);
        Destroy(mapMode);
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        _mapModeAnimator.AnimateTo(1f, Vector3.one, 0.5f);

        if (_itemCount > 0)
        {
            SetEntryFocus(SelectedIndex); // The index doesn't change, but this is important, also it seems they are reset when you fully exit the computer...
        }
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
        Vector3 targetArrowY = _entrySelectArrow.parent.InverseTransformPoint(ListItems[index].GetSelectionArrowPosition());
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
        // TODO: Maybe I can make this a better alternative:
        // item._nameField.transform.parent = item._iconRoot;
        // item._nameField.transform.SetAsFirstSibling();
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
        bool wasEmpty = _itemCount == 0;
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

        if (itemCount > 0)
        {
            if (SelectedIndex >= itemCount)
            {
                // TODO: Try to select the previous selection?
                SetEntryFocus(_itemCount - 1);
            }
            else if (wasEmpty)
            {
                // Important to reset stuff
                CustomShipLogModes.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() =>
                {
                    // TODO: A flag, run on UpdateMode, explain...
                    SetEntryFocus(0);
                });
            }
            else
            {
                // This for the case that entries were added on EnterMode for example
                UpdateListItemAlphas();
            }
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
