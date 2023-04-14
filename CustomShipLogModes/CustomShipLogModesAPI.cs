using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CustomShipLogModes;

public class CustomShipLogModesAPI : ICustomShipLogModesAPI
{
    public void AddMode(ShipLogMode mode, Func<bool> isEnabledSupplier, Func<string> nameSupplier)
    {
        CustomShipLogModes.Instance.AddMode(mode, isEnabledSupplier, nameSupplier);
    }

    public void ItemListMake(bool usePhotoAndDescField, Action<GameObject> callback)
    {
        ShipLogItemList.Make(usePhotoAndDescField, callback);
    }

    public void ItemListOpen(GameObject itemList)
    {
        itemList.GetComponent<ShipLogItemList>().Open();
    }

    public void ItemListClose(GameObject itemList)
    {
        itemList.GetComponent<ShipLogItemList>().Close();
    }

    public int ItemListUpdateList(GameObject itemList)
    {
        return itemList.GetComponent<ShipLogItemList>().UpdateList();
    }

    public void ItemListSetName(GameObject itemList, string nameValue)
    {
        itemList.GetComponent<ShipLogItemList>().SetName(nameValue);
    }

    public void ItemListSetItems(GameObject itemList, List<Tuple<string, bool, bool, bool>> items)
    {
        itemList.GetComponent<ShipLogItemList>().contentsItems = items;
    }

    public int ItemListGetSelectedIndex(GameObject itemList)
    { 
        return itemList.GetComponent<ShipLogItemList>().selectedIndex;
    }

    public void ItemListSetSelectedIndex(GameObject itemList, int index)
    {
        itemList.GetComponent<ShipLogItemList>().selectedIndex = index;
    }

    public Image ItemListGetPhoto(GameObject itemList)
    {
        return itemList.GetComponent<ShipLogItemList>().photo;
    }

    public Text ItemListGetQuestionMark(GameObject itemList)
    {
        return itemList.GetComponent<ShipLogItemList>().questionMark;
    }

    public void ItemListDescriptionFieldClear(GameObject itemList)
    {
        itemList.GetComponent<ShipLogItemList>().DescriptionFieldClear();
    }

    public ShipLogFactListItem ItemListDescriptionFieldGetNextItem(GameObject itemList)
    {
        return itemList.GetComponent<ShipLogItemList>().DescriptionFieldGetNextItem();
    }
}