using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CustomShipLogModes;

// Heavily based on ShipLogMapMode
public class ShipLogItemList : MonoBehaviour
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
    public GameObject markOnHUDPromptRoot;
    public ScreenPromptList markHUDPromptList;

    public int selectedIndex;
    public List<ShipLogEntryListItem> uiItems;
    public List<Tuple<string, bool, bool, bool>> contentsItems = new();
    public ListNavigator listNavigator;

    private bool _useDescField;

    public static void CreatePrefab(ShipLogMapMode mapMode)
    {
        // Wait a frame so the entry list item template is destroyed (otherwise issues with icons?)
        CustomShipLogModes.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() =>
        {
            GameObject prefab = Instantiate(mapMode.gameObject); // TODO: Keep each loop? What about DescriptionField?
            prefab.name = "ItemsList";
            ShipLogItemList itemList = prefab.AddComponent<ShipLogItemList>();
            itemList.oneShotSource = mapMode._oneShotSource; // Not serialized, so no in mapModeCopy, and doesn't belong to map mode

            // Copy serialized fields from MapMode TODO: Just store map mode?
            ShipLogMapMode mapModeCopy = prefab.GetComponent<ShipLogMapMode>();
            itemList.mapModeAnimator = mapModeCopy._mapModeAnimator;
            itemList.entryMenuAnimator = mapModeCopy._entryMenuAnimator;
            itemList.photo = mapModeCopy._photo;
            itemList.questionMark = mapModeCopy._questionMark;
            itemList.entrySelectArrow = mapModeCopy._entrySelectArrow;
            itemList.nameField = mapModeCopy._nameField;
            itemList.descriptionField = mapModeCopy._descriptionField;
            itemList.markOnHUDPromptRoot = mapModeCopy._markOnHUDPromptRoot;
            itemList.markHUDPromptList = mapModeCopy._markHUDPromptList;

            // By default disabled
            itemList.questionMark.gameObject.SetActive(false);
            itemList.photo.gameObject.SetActive(false); // TODO: Get parent?
            itemList.MarkHUDRootEnable(false);

            // Init animations TODO: THIS IS FAILING???
            // itemList.mapModeAnimator.SetImmediate(0f, Vector3.one * 0.5f);
            // itemList.entryMenuAnimator.SetImmediate(0f, new Vector3(1f, 0.01f, 1f));

            itemList.nameField.text = ""; // NamePanelRoot/Name

            // Init entry list
            ShipLogEntryListItem[] oldListItems = mapModeCopy._entryListRoot.GetComponentsInChildren<ShipLogEntryListItem>(true);
            itemList.uiItems = new List<ShipLogEntryListItem>();
            for (int i = 0; i < oldListItems.Length; i++)
            {
                // This are already disabled it seems, that's good, we don't want to call Update()
                // _animAlpha is already 1f
                if (i < TotalUIItems)
                {
                    itemList.uiItems.Add(oldListItems[i]);
                }
                else
                {
                    Destroy(oldListItems[i].gameObject);
                }
            }

            itemList.listNavigator = prefab.AddComponent<ListNavigator>(); // idk why a component, so its copied to instances?

            // Destroy Map Mode specific stuff
            Destroy(mapModeCopy._scaleRoot.gameObject);
            Destroy(mapModeCopy._reticleAnimator.gameObject);
            Destroy(mapModeCopy);

            // Parent object for all item lists
            GameObject commonParentGo = new GameObject("ItemsListsParent", typeof(RectTransform));
            _commonParent = commonParentGo.transform;
            _commonParent.parent = mapMode.transform.parent;
            _commonParent.localScale = Vector3.one;
            // We want to see the prompts on top of the modes! Don't use the upper right one here, since it's the one for Map Mode
            mapMode._centerPromptList.transform.parent.SetAsLastSibling();

            // Add enough room for arbitrary text in the description field
            RectTransform factList = itemList.descriptionField._factListItems[0].transform.parent as RectTransform;
            factList.offsetMax = new Vector2(0, 1_000_000);

            // Wait a frame before marking the prefab ready, so things are properly destroyed
            CustomShipLogModes.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() =>
            {
                _prefab = prefab;
            });
        });
    }

    public static void Make(bool usePhoto, bool useDescField, Action<MonoBehaviour> callback)
    {
        CustomShipLogModes.Instance.ModHelper.Events.Unity.RunWhen(() => _prefab != null, () =>
        {
            GameObject itemListModeGo = Instantiate(_prefab, _commonParent.parent);
            itemListModeGo.transform.SetParent(_commonParent, true); // I would like to set the parent on Instantiate but idk
            ShipLogItemList itemList = itemListModeGo.GetComponent<ShipLogItemList>();
            itemList._useDescField = useDescField;
            if (!usePhoto || !useDescField)
            {
                // TODO: Changeable?
                itemList.HidePhotoOrDescField(usePhoto, useDescField);
            }
            callback.Invoke(itemList);
        });
    }

    public void HidePhotoOrDescField(bool usePhoto, bool useDescField)
    {
        RectTransform entryListRoot = (RectTransform)uiItems[0].transform.parent.parent;

        if (!usePhoto)
        {
            // Hide photo (root) and expand entry list horizontally
            questionMark.transform.parent.gameObject.SetActive(false); // photo need an extra parent now...
            // Needed after OW patch 14, arrow no longer anchored to the right
            Vector3 prevArrowPosition = entrySelectArrow.localPosition;
            // idk this seems to work
            entryListRoot.anchorMax = new Vector2(1, 1);
            entryListRoot.offsetMax = new Vector2(0, 0);
            entrySelectArrow.localPosition = prevArrowPosition;
        }

        if (!useDescField)
        {
            // Expand vertically because we don't currently use description field
            // Magic number to match the bottom line with the description field, idk how to properly calculate it
            RectTransform entryMenu = entryListRoot.parent as RectTransform; // Could also get from mapMode._entryMenuAnimator
            entryMenu.offsetMin = new Vector2(entryMenu.offsetMin.x, -594);
        }
    }

    public void Open()
    {
        mapModeAnimator.AnimateTo(1f, Vector3.one, 0.5f);
        entryMenuAnimator.AnimateTo(1f, Vector3.one, 0.3f);

        if (_useDescField)
        {
            descriptionField.SetVisible(true);
        }
    }

    public void Close()
    {
        mapModeAnimator.AnimateTo(0f, Vector3.one * 0.5f, 0.5f);
        entryMenuAnimator.AnimateTo(0f, new Vector3(1f, 0.01f, 1f), 0.3f);
        if (_useDescField)
        {
            descriptionField.SetVisible(false);
        }
    }

    public void UpdateListUI()
    {
        // Keep the same scrolling behaviour as Map Mode but with fixed UI elements that we populate, like in Suit Log
        int shownItems = _useDescField ? TotalUIItemsWithDescriptionField : TotalUIItems;
        int lastSelectable = 4; // Scroll after that  // TODO: More for without desc field?
        int itemIndexOnTop = selectedIndex <= lastSelectable ? 0 : selectedIndex - lastSelectable;
        for (int i = 0; i < uiItems.Count; i++)
        {
            ShipLogEntryListItem uiItem = uiItems[i];
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

        // Make sure to hide the arrow if no items are available
        entrySelectArrow.gameObject.SetActive(contentsItems.Count > 0);
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
    
    public int UpdateList()
    {
        int selectionChange  = 0;

        if (contentsItems.Count >= 2)
        {
            selectionChange = listNavigator.GetSelectionChange();
            if (selectionChange != 0)
            {
                selectedIndex += selectionChange;
                if (selectedIndex == -1)
                {
                    selectedIndex = contentsItems.Count - 1;
                }
                else if (selectedIndex == contentsItems.Count)
                {
                    selectedIndex = 0;
                }
                oneShotSource.PlayOneShot(AudioType.ShipLogMoveBetweenEntries);
            }
        }

        UpdateListUI();

        return selectionChange;
    }

    public void SetName(string nameValue)
    {
        nameField.text = nameValue;
    }

    public void DescriptionFieldClear()
    {
        // Very similar to SetText but without text...
        descriptionField.ResetListPos();
        descriptionField._link = null;
        descriptionField._entry = null;
        descriptionField._displayCount = 0;
        foreach (ShipLogFactListItem item in descriptionField._factListItems)
        {
            item.Clear();
        }
    }

    public ShipLogFactListItem DescriptionFieldGetNextItem()
    {
        int nextIndex = descriptionField._displayCount;
        descriptionField._displayCount++;
        if (nextIndex == descriptionField._factListItems.Length)
        {
            // Create a new item, in vanilla there are 10 but a mod could use more, we don't have the template so use the 0
            ShipLogFactListItem newItem = Instantiate(descriptionField._factListItems[0], descriptionField._factListItems[0].transform.parent);
            newItem.name = "FactListItem_" + nextIndex;
            Array.Resize(ref descriptionField._factListItems, nextIndex + 1);
            descriptionField._factListItems[nextIndex] = newItem;
            // newItem.RegisterWithFontAndLanguageController(descriptionField._fontAndLanguageController); // just in case... TODO: Not used anymore?
            // TODO: NRE next ShipLogEntryDescriptionField update???
            // TODO: Patch DisplayText DisplayFacts, to clear the rest!
        }
        ShipLogFactListItem nextItem = descriptionField._factListItems[nextIndex];
        nextItem.DisplayText(string.Empty);
        return nextItem;
    }

    public void MarkHUDRootEnable(bool enable)
    {
        markOnHUDPromptRoot.gameObject.SetActive(enable);
    }

    public ScreenPromptList MarkHUDGetPromptList()
    {
        return markHUDPromptList;
    }

    public int GetIndexUI(int index)
    {
        // This is copied from UpdateListUI()...
        int shownItems = _useDescField ? TotalUIItemsWithDescriptionField : TotalUIItems;
        int lastSelectable = 4;
        int itemIndexOnTop = selectedIndex <= lastSelectable ? 0 : selectedIndex - lastSelectable;

        int uiIndex = index - itemIndexOnTop;
        return uiIndex < shownItems ? uiIndex : -1;
    }
}
