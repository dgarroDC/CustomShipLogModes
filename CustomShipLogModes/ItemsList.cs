using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CustomShipLogModes;

// TODO: Check this._listItems[j].Reset();??? _hasFocus for example
// TODO: ShipLogEntryDescriptionFieldUtils : GetNext, Reset, ???
// TODO: Ship_Body/Module_Cabin/Systems_Cabin/ShipLogPivot/ShipLog/ShipLogPivot/ShipLogCanvas/MapMode/EntryMenu/PhotoRoot/MarkHUDRoot/

// Heavily based on ShipLogMapMode
// TODO: API return id (or GO?) for new "UI list"? + API methods for all actions  
public class ItemsList : MonoBehaviour
{
    private static GameObject _prefab; // TODO: Only one and switch? Although we want to allow any customization
    private static ShipLogMapMode _original;

    private const int TotalUIItems = 14;
    private const int TotalUIItemsWithDescriptionField = 7; // One more could fit, but this is the vanilla way

    // From ShipLogMapMode
    public OWAudioSource oneShotSource; 
    public CanvasGroupAnimator mapModeAnimator;
    public CanvasGroupAnimator entryMenuAnimator;
    public RectTransform entrySelectArrow;
    public Text nameField;
    public FontAndLanguageController fontAndLanguageController; // TODO: Do we really need this? Not used?
    public Image photo;
    public Text questionMark;
    public ShipLogEntryDescriptionField descriptionField;

    public int SelectedIndex;
    protected List<ShipLogEntryListItem> ListItems; // TODO: Rename _uiItems?
    public List<string> ContentsItems = new(); // TODO: Rename ListItems?

    // TODO: All public?
    private bool _usePhotoAndDescField;
    private ListNavigator _listNavigator;

    // TODO: Let other mods know when is this ready?
    public static void CreatePrefab(ShipLogMapMode mapMode)
    {
        _original = mapMode;
        _prefab = Instantiate(mapMode.gameObject); // TODO: Keep each loop? What about DescriptionField?
        ItemsList itemsList = _prefab.AddComponent<ItemsList>();
        itemsList.oneShotSource = _original._oneShotSource; // Not serialized, so no in mapModeCopy, and doesn't belong to map mode

        // Copy serialized fields from MapMode TODO: Just store map mode?
        ShipLogMapMode mapModeCopy = _prefab.GetComponent<ShipLogMapMode>();
        itemsList.mapModeAnimator = mapModeCopy._mapModeAnimator;
        itemsList.entryMenuAnimator = mapModeCopy._entryMenuAnimator;
        itemsList.photo = mapModeCopy._photo;
        itemsList.questionMark = mapModeCopy._questionMark;
        itemsList.entrySelectArrow = mapModeCopy._entrySelectArrow;
        itemsList.nameField = mapModeCopy._nameField;
        itemsList.fontAndLanguageController = mapModeCopy._fontAndLanguageController;
        itemsList.descriptionField = mapModeCopy._descriptionField; // This could also be from _original, same object


    }

    public static GameObject Make(bool usePhotoAndDescField)
    {
        // TODO: Somehow do this after ShipLogMapMode.Initialize? Reuse entry list and GetMapMode() instead of Find...
        // TODO: CHECK IF PREFAB IS NULL
        GameObject itemListModeGo = Instantiate(_prefab, _original.transform.position, _original.transform.rotation, _original.transform.parent);
        _original._upperRightPromptList.transform.parent.SetAsLastSibling(); // We want to see the prompts on top of the mode! TODO: Make a common parent object for that!
        ItemsList itemsList = itemListModeGo.GetComponent<ItemsList>();
        itemsList._usePhotoAndDescField = usePhotoAndDescField;
        return itemListModeGo;
    }

    public void Initialize()
    {
        ShipLogMapMode mapMode = gameObject.GetComponent<ShipLogMapMode>();

        // Init animations (allow changing?)
        mapModeAnimator.SetImmediate(0f, Vector3.one * 0.5f);
        entryMenuAnimator.SetImmediate(0f, new Vector3(1f, 0.01f, 1f));

        // TODO: Changeable?
        if (!_usePhotoAndDescField)
        {
            // Hide photo (root) and expand entry list horizontally
            Transform photoRoot = mapMode._photoRoot;
            photoRoot.gameObject.SetActive(false);
            // idk this seems to work
            RectTransform entryListRoot = (RectTransform)mapMode._entryListRoot.parent;
            entryListRoot.anchorMax = new Vector2(1, 1);
            entryListRoot.offsetMax = new Vector2(0, 0);
            
            // Expand vertically because we don't currently use description field
            // Magic number to match the bottom line with the description field, idk how to properly calculate it
            RectTransform entryMenu = entryListRoot.parent as RectTransform; // Could also get from mapMode._entryMenuAnimator
            entryMenu.offsetMin = new Vector2(entryMenu.offsetMin.x, -594);
        }
        else
        {
            // Don't start with Map Mode's last viewed image (although the implementer could just cover this...)
            Texture2D texture = Texture2D.blackTexture;
            photo.sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            photo.gameObject.SetActive(false);
            questionMark.gameObject.SetActive(false);
        }

        // nameField.font = Locator.GetUIStyleManager().GetShipLogFont(); // TODO: Probably not needed, but ShipLogMapMode does it, but it looks off...
        nameField.text = ""; // NamePanelRoot/Name

        // Init entry list
        ShipLogEntryListItem[] oldListItems = mapMode._entryListRoot.GetComponentsInChildren<ShipLogEntryListItem>(true);
        ListItems = new List<ShipLogEntryListItem>();
        // TODO: Only do for Total... Destroy the rest? Make sure it's ok to instantiate the prefab on same frame
        for (int i = 0; i < oldListItems.Length; i++)
        {
            // TODO: If this run while map mode is open entries with small font are back to default size? Reel player but not ModeA?
            SetupAndAddItem(oldListItems[i]);
        }
        _listNavigator = new ListNavigator();

        // Hide/Destroy Map Mode specific stuff
        mapMode._scaleRoot.gameObject.SetActive(false);
        mapMode._reticleAnimator.gameObject.SetActive(false);
        Destroy(mapMode);
    }

    public void Open()
    {
        mapModeAnimator.AnimateTo(1f, Vector3.one, 0.5f);
        entryMenuAnimator.AnimateTo(1f, Vector3.one, 0.3f);

        if (_usePhotoAndDescField)
        {
            descriptionField.SetText("TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT TEST TEXT");
            descriptionField.SetVisible(true);
        }
    }

    public void Close()
    {
        mapModeAnimator.AnimateTo(0f, Vector3.one * 0.5f, 0.5f);
        entryMenuAnimator.AnimateTo(0f, new Vector3(1f, 0.01f, 1f), 0.3f);
        if (_usePhotoAndDescField)
        {
            descriptionField.SetVisible(false);
        }
    }

    // TODO: The checks should be done on navigating... Remove this method
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
        SelectedIndex = index;
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
                    Vector3 origArrowPos = entrySelectArrow.localPosition;
                    Vector3 targetArrowY = entrySelectArrow.parent.InverseTransformPoint(uiItem.GetSelectionArrowPosition());
                    entrySelectArrow.localPosition = new Vector3(origArrowPos.x, targetArrowY.y, origArrowPos.z);
                }

                // TODO: Icons, use ShipLogEntry?
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
        item.Init(fontAndLanguageController);
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

    public bool UpdateList()
    {
        bool selectionChanged = false;

        if (ContentsItems.Count >= 2)
        {
            int selectionChange = _listNavigator.GetSelectionChange(); // TODO: Return this or boolean to user (although it could just check if selected changed
            if (selectionChange != 0)
            {
                selectionChanged = true;
                SetEntryFocus(SelectedIndex + selectionChange);
                // Don't play sound in SetEntryFocus to avoid playing it in some situations
                oneShotSource.PlayOneShot(AudioType.ShipLogMoveBetweenEntries);
            }
        }

        UpdateListUI();

        return selectionChanged;
    }

    public void SetName(string nameValue)
    {
        nameField.text = nameValue;
    }
}
