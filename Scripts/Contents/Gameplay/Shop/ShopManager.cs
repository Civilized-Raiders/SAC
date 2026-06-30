using Fusion;
using UnityEngine;

/// <summary>  
/// 상점에서 아이템을 구매/판매, 현재 팔 업그레이드, 골드 획득/차감 기능을 구현한 스크립트  
/// </summary>  
public class ShopManager : NetworkBehaviour
{
    private static ShopManager _instance;
    public static ShopManager Instance { get; private set; }

    [SerializeField] GameObject dropPoint;
    public override void Spawned()
    {

        if (Instance != null && Instance != this)
        {
            Runner.Despawn(Object);
            return;
        }

        Instance = this;
        
    }
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if(Instance ==this) Instance = null;
    }

    public bool BuyItem(ShopItemData item)
    {
        if (item == null) return false;

        if (!item.itemIPrefabRef.IsValid)
            return false;

        Vector3 drop = dropPoint.transform.position;

        if (Object != null && Object.HasStateAuthority)
        {
            return TryBuyAndSpawn(item.itemBuyPrice, item.itemName, item.itemIPrefabRef, drop);
        }

        RpcRequestBuy(item.itemBuyPrice, item.itemName, item.itemIPrefabRef, drop);
        return true; // 요청 전송 성공. 실제 구매 성공 여부는 호스트에서 결정됨.
    }
    private bool TryBuyAndSpawn(int price, string itemName, NetworkPrefabRef prefabRef, Vector3 drop)
    {
        if (NetworkGameManager.Instance == null) return false;

        bool success = NetworkGameManager.Instance.TryConsumeMoney(price);
        if (!success)
        {
            Debug.Log($"골드 부족: {itemName} 구매 실패 (필요 골드: {price})");
            return false;
        }
        Runner.Spawn(prefabRef, drop, Quaternion.identity, inputAuthority: null);
        Debug.Log($"{itemName} 구매 완료");
        return true;
    }
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcRequestBuy(int price, string itemName, NetworkPrefabRef prefabRef, Vector3 drop)
    {
        TryBuyAndSpawn(price, itemName, prefabRef, drop);
    }
    public void SellItem(ShopItemData item)
    {
        if (item == null) return;

        NetworkGameManager.Instance.AddMoney(item.itemSellPrice);
        Debug.Log($"{item.itemName} 판매 완료 (+{item.itemSellPrice} 골드)");
    }
    
}
