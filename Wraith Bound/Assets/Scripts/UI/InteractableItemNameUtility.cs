using UnityEngine;

public static class InteractableItemNameUtility
{
    public static bool TryGetItemName(Collider collider, out string itemName)
    {
        itemName = null;

        if (collider == null)
        {
            return false;
        }

        ItemObject itemObject = collider.GetComponentInParent<ItemObject>();
        if (itemObject != null && itemObject.itemData != null)
        {
            itemName = itemObject.itemData.itemName;
            return !string.IsNullOrEmpty(itemName);
        }

        InteractableItem interactableItem = collider.GetComponentInParent<InteractableItem>();
        if (interactableItem != null && interactableItem.TryGetItemData(out Items itemData))
        {
            itemName = itemData.itemName;
            return !string.IsNullOrEmpty(itemName);
        }

        return false;
    }
}
