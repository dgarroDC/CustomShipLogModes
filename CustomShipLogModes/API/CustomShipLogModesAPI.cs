using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CustomShipLogModes.API;

public class CustomShipLogModesAPI : ICustomShipLogModesAPI
{
    public void AddMode(ShipLogMode mode, Func<bool> isEnabledSupplier, Func<string> nameSupplier)
    {
        CustomShipLogModes.Instance.AddMode(mode, isEnabledSupplier, nameSupplier);
    }

    public void ItemListMake(bool usePhotoAndDescField, Action<MonoBehaviour> callback)
    {
        ShipLogItemList.Make(usePhotoAndDescField, callback);
    }

    public void ItemListOpen(MonoBehaviour itemList)
    {
        ((ShipLogItemList)itemList).Open();
    }

    public void ItemListClose(MonoBehaviour itemList)
    {
        ((ShipLogItemList)itemList).Close();
    }

    public int ItemListUpdateList(MonoBehaviour itemList)
    {
        return ((ShipLogItemList)itemList).UpdateList();
    }

    public void ItemListSetName(MonoBehaviour itemList, string nameValue)
    {
        ((ShipLogItemList)itemList).SetName(nameValue);
    }

    public void ItemListSetItems(MonoBehaviour itemList, List<Tuple<string, bool, bool, bool>> items)
    {
        ((ShipLogItemList)itemList).contentsItems = items;
    }

    public int ItemListGetSelectedIndex(MonoBehaviour itemList)
    { 
        return ((ShipLogItemList)itemList).selectedIndex;
    }

    public void ItemListSetSelectedIndex(MonoBehaviour itemList, int index)
    {
        ((ShipLogItemList)itemList).selectedIndex = index;
    }

    public Image ItemListGetPhoto(MonoBehaviour itemList)
    {
        return ((ShipLogItemList)itemList).photo;
    }

    public Text ItemListGetQuestionMark(MonoBehaviour itemList)
    {
        return ((ShipLogItemList)itemList).questionMark;
    }

    public void ItemListDescriptionFieldClear(MonoBehaviour itemList)
    {
        ((ShipLogItemList)itemList).DescriptionFieldClear();
    }

    public ShipLogFactListItem ItemListDescriptionFieldGetNextItem(MonoBehaviour itemList)
    {
        return ((ShipLogItemList)itemList).DescriptionFieldGetNextItem();
    }
}
