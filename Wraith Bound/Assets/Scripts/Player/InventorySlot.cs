using Unity.XR.OpenVR;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public class InventorySlot
{
    public Items item;
    public int amount;

    public InventorySlot(Items item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    public void AddAmount(int value)
    {
        amount += value;
    }

    public void RemoveAmount(int value)
    {
        amount -= value;
    }
}

public class Inventory : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
