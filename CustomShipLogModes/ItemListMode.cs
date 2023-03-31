using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

namespace CustomShipLogModes;

// TODO: Check this._listItems[j].Reset();??? _hasFocus for example
// TODO: ShipLogEntryDescriptionFieldUtils : GetNext, Reset, ???
// TODO: Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode/EntryMenu/PhotoRoot/MarkHUDRoot/

// Heavily based on ShipLogMapMode
// TODO: Non abstract? Wraper? Not a mode? API return id (or GO?) for new "UI list"? + API methods for all actions  
public class ItemListMode : MonoBehaviour
{
    private static GameObject _prefab; // TODO: Only one and switch? Although we want to allow any customization
    private static ShipLogMapMode _original;

    private const int TotalUIItems = 14;
    private const int TotalUIItemsWithDescriptionField = 7; // One more could fit, but this is the vanilla way

    private OWAudioSource _oneShotSource;

    public int SelectedIndex;
    protected List<ShipLogEntryListItem> ListItems; // TODO: Rename _uiItems?
    public List<string> ContentsItems = new(); // TODO: Rename ListItems?

    protected Image Photo;
    protected Text QuestionMark;
    protected ShipLogEntryDescriptionField DescriptionField;

    private bool _usePhotoAndDescField;
   // TODO: All public?
    private CanvasGroupAnimator _mapModeAnimator;
    private CanvasGroupAnimator _entryMenuAnimator;
    private RectTransform _entryListRoot;
    private ListNavigator _listNavigator;
    private RectTransform _entrySelectArrow;
    private FontAndLanguageController _fontAndLanguageController; // Do we really need this?
    private Text _nameField;

    // TODO: Let other mods know when is this ready?
    public static void CreatePrefab(ShipLogMapMode mapMode)
    {
        _original = mapMode;
        _prefab = Instantiate(mapMode.gameObject); // TODO: Keep each loop? What about DescriptionField?
        // TODO: Somehow do the Initialize just one for the prefab? Although what about subclass?
    }

    public static GameObject Make(bool usePhotoAndDescField)
    {
        // TODO: Somehow do this after ShipLogMapMode.Initialize? Reuse entry list and GetMapMode() instead of Find...
        // TODO: CHECK IF PREFAB IS NULL
        GameObject itemListModeGo = Instantiate(_prefab, _original.transform.position, _original.transform.rotation, _original.transform.parent);
        ItemListMode itemListMode = itemListModeGo.AddComponent<ItemListMode>();
        itemListMode._usePhotoAndDescField = usePhotoAndDescField;
        return itemListModeGo;
        // TODO: Fix that if you run this after map mode init then the icons are in wrong place? ALSO ALPHA?
        // TODO: it's ultra BROKEN if Make is run in same frame after Destroy of map mode entry template!!! Solution? Keep only the last entry? But why?
        // TODO: Also broken if map mode had selected entry > n (list scrolled) on copy, _origEntryListPos would be wrong! Copy _origEntryListPos from Map mode?
    }

    protected virtual void OnItemSelected()
    {
        // No-op
        // TODO: On enter? Index starting in -1?
    }

    public void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        // TODO: Get all of these from _original?
        _oneShotSource = oneShotSource;
        upperRightPromptList.transform.parent.SetAsLastSibling(); // We want to see the prompts on top of the mode!

        ShipLogMapMode mapMode = gameObject.GetComponent<ShipLogMapMode>();
        _entryListRoot = mapMode._entryListRoot; // /EntryMenu/EntryListRoot/EntryList

        // Init animations (allow changing?)
        _mapModeAnimator = mapMode._mapModeAnimator;
        _mapModeAnimator.SetImmediate(0f, Vector3.one * 0.5f);
        _entryMenuAnimator = mapMode._entryMenuAnimator;
        _entryMenuAnimator.SetImmediate(0f, new Vector3(1f, 0.01f, 1f));

        // TODO: Changeable?
        if (!_usePhotoAndDescField)
        {
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
            DescriptionField = mapMode._descriptionField;
        }

        _fontAndLanguageController = mapMode._fontAndLanguageController;
        // nameField.font = Locator.GetUIStyleManager().GetShipLogFont(); // TODO: Probably not needed, but ShipLogMapMode does it, but it looks off...
        _nameField = mapMode._nameField;
        _nameField.text = ""; // NamePanelRoot/Name

        // Init entry list
        ShipLogEntryListItem[] oldListItems = _entryListRoot.GetComponentsInChildren<ShipLogEntryListItem>(true);
        ListItems = new List<ShipLogEntryListItem>();
        // TODO: Only do for Total... Destroy the rest? Make sure it's ok to instantiate the prefab on same frame
        for (int i = 0; i < oldListItems.Length; i++)
        {
            // TODO: If this run while map mode is open entries with small font are back to default size? Reel player but not ModeA?
            SetupAndAddItem(oldListItems[i]);
        }
        _entrySelectArrow = mapMode._entrySelectArrow;
        _listNavigator = new ListNavigator();

        // Hide/Destroy Map Mode specific stuff
        mapMode._scaleRoot.gameObject.SetActive(false);
        mapMode._reticleAnimator.gameObject.SetActive(false);
        Destroy(mapMode);
    }

    public void Open()
    {
        _mapModeAnimator.AnimateTo(1f, Vector3.one, 0.5f);
        _entryMenuAnimator.AnimateTo(1f, Vector3.one, 0.3f);

        if (_usePhotoAndDescField)
        {
            DescriptionField.SetText("TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT");
            DescriptionField.SetVisible(true);
        }

        if (ContentsItems.Count > 0)
        {
            SetEntryFocus(SelectedIndex); // The index doesn't change, but this is important, also it seems they (alphas?) are reset when you fully exit the computer...
        }
    }

    public void Close()
    {
        _mapModeAnimator.AnimateTo(0f, Vector3.one * 0.5f, 0.5f);
        _entryMenuAnimator.AnimateTo(0f, new Vector3(1f, 0.01f, 1f), 0.3f);
        DescriptionField?.SetVisible(false);
    }

    // TODO: Callable from API? Although it could just be assigned to SelectedIndex
    protected void SetEntryFocus(int index)
    {
        if (index == -1)
        {
            index = ContentsItems.Count - 1; // Important to use the item list here, not the entry list!!!
        }
        else if (index == ContentsItems.Count)
        {
            index = 0;
        }
        //
        // int topIndex = Mathf.Max(0, index - 4);
        // if (topIndex == 0)
        // {
        //     // TODO: Remove this case, just create some items on setup..
        //     _entryListRoot.anchoredPosition = _origEntryListPos;
        // }
        // else
        // {
        //     // There are at least two items, so there are at least two UI items
        //     float itemsSpace = ListItems[1].gameObject.GetComponent<RectTransform>().anchoredPosition.y -
        //                        ListItems[0].gameObject.GetComponent<RectTransform>().anchoredPosition.y;    
        //     _entryListRoot.anchoredPosition = _origEntryListPos - new Vector2(0f, topIndex * itemsSpace);
        // }
        //
        // Vector3 origArrowPos = _entrySelectArrow.localPosition;
        // Vector3 targetArrowY = _entrySelectArrow.parent.InverseTransformPoint(ListItems[index].GetSelectionArrowPosition());
        // _entrySelectArrow.localPosition = new Vector3(origArrowPos.x, targetArrowY.y, origArrowPos.z);

        SelectedIndex = index;
        OnItemSelected();
    }

    // TODO: Test with 0, 1 items: this._entrySelectArrow.gameObject.SetActive(list.Count > 0);
    private void UpdateListUI()
    {
        // Keep the same scrolling behaviour as Map Mode but with fixed UI elements that we populate, like in Suit Log
        int shownItems = _usePhotoAndDescField ? TotalUIItemsWithDescriptionField : TotalUIItems;
        int lastSelectable = 4; // Scroll after that  // TODO: More for without desc field?
        int itemIndexOnTop = SelectedIndex <= lastSelectable ? 0 : SelectedIndex - lastSelectable;
        for (int i = 0; i < ListItems.Count; i++)
        {
            ShipLogEntryListItem uiItem = ListItems[i];
            int itemIndex = itemIndexOnTop + i;
            if (itemIndex < ContentsItems.Count && i < shownItems) // TODO: No need to iterate all?
            {
                uiItem._nameField.text = ContentsItems[itemIndex];
                SetFocus(uiItem, itemIndex == SelectedIndex);
                if (itemIndex == SelectedIndex)
                {
                    // Arrow
                    Vector3 origArrowPos = _entrySelectArrow.localPosition;
                    Vector3 targetArrowY = _entrySelectArrow.parent.InverseTransformPoint(uiItem.GetSelectionArrowPosition());
                    _entrySelectArrow.localPosition = new Vector3(origArrowPos.x, targetArrowY.y, origArrowPos.z);
                }

                // TODO: Icons, use ShipLogEntry?
                // TODO: Arrow
                float listAlpha = 1f;
                // This replicates the vanilla look, entries with index 6 (last), 5 and 4 have this alphas,
                // although is weird that 4 also has that alpha even if selected
                // We interpret it as "the last" entries so in the mode without desc field we also use these values
                // for the last 3 displayed items
                if (i == shownItems - 1)
                {
                    listAlpha = 0.05f;
                }
                else if (i == shownItems -2)
                {
                    listAlpha = 0.2f;
                }
                else if (i == shownItems - 3)
                {
                    listAlpha = 0.5f;
                }
                uiItem.SetListAlpha(listAlpha);
                uiItem.gameObject.SetActive(true);
            }
            else
            {
                uiItem.gameObject.SetActive(false);
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

    public void UpdateList()
    {
        // TODO: I'm sure we can remove this now!
        if (DescriptionField != null) DescriptionField._factListItems = DescriptionField.GetComponentsInChildren<ShipLogFactListItem>(true);

        if (ContentsItems.Count >= 2)
        {
            int selectionChange = _listNavigator.GetSelectionChange(); // TODO: Return this or boolean to user (although it could just check if selected changed
            if (selectionChange != 0)
            {
                SetEntryFocus(SelectedIndex + selectionChange);
                // Don't play sound in SetEntryFocus to avoid playing it in some situations
                _oneShotSource.PlayOneShot(AudioType.ShipLogMoveBetweenEntries);
            }
        }

        UpdateListUI();
    }

    public void SetName(string nameValue)
    {
        _nameField.text = nameValue;
    }
}
