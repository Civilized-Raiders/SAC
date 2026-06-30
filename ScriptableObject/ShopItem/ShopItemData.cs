using Fusion;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "ShopItemData", menuName = "Scriptable Objects/ShopItemData")]
public class ShopItemData : ScriptableObject
{
    public int itemCode;
    public string itemName;
    public int itemBuyPrice;
    public int itemSellPrice;
    public Sprite itemIcon;
    public NetworkPrefabRef itemIPrefabRef;
    [TextArea] public string itemDescription;
}