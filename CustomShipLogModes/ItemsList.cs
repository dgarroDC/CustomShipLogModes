using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CustomShipLogModes;

// TODO: ShipLogEntryDescriptionFieldUtils : GetNext, Reset, ???
// Heavily based on ShipLogMapMode
// TODO: API return id (or GO?) for new "UI list"? + API methods for all actions  
public class ItemsList : MonoBehaviour
{
    private static GameObject _prefab;
    private static Transform _commonParent;

    private const int TotalUIItems = 14;
    private const int TotalUIItemsWithDescriptionField = 7; // One more could fit, but this is the vanilla way

    // From ShipLogMapMode
    public OWAudioSource oneShotSource; 
    public CanvasGroupAnimator mapModeAnimator;
    public CanvasGroupAnimator entryMenuAnimator;
    public RectTransform entrySelectArrow;
    public Text nameField;
    public Image photo;
    public Text questionMark;
    public ShipLogEntryDescriptionField descriptionField;

    public int selectedIndex;
    public List<ShipLogEntryListItem> listItems; // TODO: Rename uiItems?
    public List<Tuple<string, bool, bool, bool>> contentsItems = new(); // TODO: Rename listItems?
    public ListNavigator listNavigator;

    // public?
    private bool _usePhotoAndDescField;

    // TODO: Let other mods know when is this ready?
    public static void CreatePrefab(ShipLogMapMode mapMode)
    {
        // Wait a frame so the entry list item template is destroyed (otherwise issues with icons?)
        CustomShipLogModes.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() =>
        {
            GameObject prefab = Instantiate(mapMode.gameObject); // TODO: Keep each loop? What about DescriptionField?
            prefab.name = "ItemsList";
            ItemsList itemsList = prefab.AddComponent<ItemsList>();
            itemsList.oneShotSource = mapMode._oneShotSource; // Not serialized, so no in mapModeCopy, and doesn't belong to map mode

            // Copy serialized fields from MapMode TODO: Just store map mode?
            ShipLogMapMode mapModeCopy = prefab.GetComponent<ShipLogMapMode>();
            itemsList.mapModeAnimator = mapModeCopy._mapModeAnimator;
            itemsList.entryMenuAnimator = mapModeCopy._entryMenuAnimator;
            itemsList.photo = mapModeCopy._photo;
            itemsList.questionMark = mapModeCopy._questionMark;
            itemsList.entrySelectArrow = mapModeCopy._entrySelectArrow;
            itemsList.nameField = mapModeCopy._nameField;
            itemsList.descriptionField = mapModeCopy._descriptionField; // This could also be from _original, same object

            // Init animations TODO: allow changing?
            itemsList.mapModeAnimator.SetImmediate(0f, Vector3.one * 0.5f);
            itemsList.entryMenuAnimator.SetImmediate(0f, new Vector3(1f, 0.01f, 1f));

            itemsList.nameField.text = ""; // NamePanelRoot/Name

            // Init entry list
            ShipLogEntryListItem[] oldListItems = mapModeCopy._entryListRoot.GetComponentsInChildren<ShipLogEntryListItem>(true);
            itemsList.listItems = new List<ShipLogEntryListItem>();
            // TODO: Only do for Total... Destroy the rest? Make sure it's ok to instantiate the prefab on same frame
            for (int i = 0; i < oldListItems.Length; i++)
            {
                // This are already disabled it seems, that's good, we don't want to call Update()
                // _animAlpha is already 1f
                if (i < TotalUIItems)
                {
                    itemsList.listItems.Add(oldListItems[i]);
                }
                else
                {
                    Destroy(oldListItems[i].gameObject);
                }
            }

            itemsList.listNavigator = prefab.AddComponent<ListNavigator>(); // idk why a component, so its copied to instances?

            // Destroy Map Mode specific stuff
            Destroy(mapModeCopy._scaleRoot.gameObject);
            Destroy(mapModeCopy._reticleAnimator.gameObject);
            Destroy(mapModeCopy._markOnHUDPromptRoot.gameObject); // TODO: Keep this?
            Destroy(mapModeCopy);

            // Parent object for all item lists
            GameObject commonParentGo = new GameObject("ItemsListsParent", typeof(RectTransform));
            _commonParent = commonParentGo.transform;
            _commonParent.parent = mapMode.transform.parent;
            _commonParent.localScale = Vector3.one;
            mapMode._upperRightPromptList.transform.parent.SetAsLastSibling(); // We want to see the prompts on top of the modes!

            // Wait a frame before marking the prefab ready, so things are properly destroyed
            CustomShipLogModes.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() =>
            {
                _prefab = prefab;
            });
        });
    }

    public static void Make(bool usePhotoAndDescField, Action<GameObject> callback)
    {
        CustomShipLogModes.Instance.ModHelper.Events.Unity.RunWhen(() => _prefab != null, () =>
        {
            GameObject itemListModeGo = Instantiate(_prefab, _commonParent.parent);
            itemListModeGo.transform.SetParent(_commonParent, true); // I would like to set the parent on Instantiate but idk
            ItemsList itemsList = itemListModeGo.GetComponent<ItemsList>();
            itemsList._usePhotoAndDescField = usePhotoAndDescField;
            if (!usePhotoAndDescField)
            {
                // TODO: Changeable?
                itemsList.HidePhotoAndDescField();
            }
            callback.Invoke(itemListModeGo);
        });
    }

    public void HidePhotoAndDescField()
    {
        // Hide photo (root) and expand entry list horizontally
        photo.transform.parent.gameObject.SetActive(false);
        // idk this seems to work
        RectTransform entryListRoot = (RectTransform)listItems[0].transform.parent.parent;
        entryListRoot.anchorMax = new Vector2(1, 1);
        entryListRoot.offsetMax = new Vector2(0, 0);
            
        // Expand vertically because we don't currently use description field
        // Magic number to match the bottom line with the description field, idk how to properly calculate it
        RectTransform entryMenu = entryListRoot.parent as RectTransform; // Could also get from mapMode._entryMenuAnimator
        entryMenu.offsetMin = new Vector2(entryMenu.offsetMin.x, -594);
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
            index = contentsItems.Count - 1; // Important to use the item list here, not the entry list!!!
        }
        else if (index == contentsItems.Count)
        {
            index = 0;
        }
        selectedIndex = index;
    }

    // TODO: Test with 0, 1 items: this._entrySelectArrow.gameObject.SetActive(list.Count > 0);
    private void UpdateListUI()
    {
        // Keep the same scrolling behaviour as Map Mode but with fixed UI elements that we populate, like in Suit Log
        int shownItems = _usePhotoAndDescField ? TotalUIItemsWithDescriptionField : TotalUIItems;
        int lastSelectable = 4; // Scroll after that  // TODO: More for without desc field?
        int itemIndexOnTop = selectedIndex <= lastSelectable ? 0 : selectedIndex - lastSelectable;
        for (int i = 0; i < listItems.Count; i++)
        {
            ShipLogEntryListItem uiItem = listItems[i];
            int itemIndex = itemIndexOnTop + i;
            if (itemIndex < contentsItems.Count && i < shownItems) // TODO: No need to iterate all?
            {
                Tuple<string,bool,bool,bool> item = contentsItems[itemIndex];
                uiItem._nameField.text = item.Item1;
                SetFocus(uiItem, itemIndex == selectedIndex);
                if (itemIndex == selectedIndex)
                {
                    // Arrow
                    Vector3 origArrowPos = entrySelectArrow.localPosition;
                    Vector3 targetArrowY = entrySelectArrow.parent.InverseTransformPoint(uiItem.GetSelectionArrowPosition());
                    entrySelectArrow.localPosition = new Vector3(origArrowPos.x, targetArrowY.y, origArrowPos.z);
                }

                // Icons
                uiItem._hudMarkerIcon.gameObject.SetActive(item.Item2);
                uiItem._unreadIcon.gameObject.SetActive(item.Item3);
                uiItem._moreToExploreIcon.gameObject.SetActive(item.Item4);
                
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
        if (item._hasFocus != focus || item._focusAlpha == 0f) // Second check for first time
        {
            // The _hasFocus is to avoid changing the alpha in unnecessary cases maybe...
            item._hasFocus = focus;
            item._focusAlpha = focus ? 1f : 0.2f;
            item.UpdateAlpha();
        }
    }
    
    public bool UpdateList()
    {
        bool selectionChanged = false;

        if (contentsItems.Count >= 2)
        {
            int selectionChange = listNavigator.GetSelectionChange(); // TODO: Return this or boolean to user (although it could just check if selected changed
            if (selectionChange != 0)
            {
                selectionChanged = true;
                SetEntryFocus(selectedIndex + selectionChange);
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
