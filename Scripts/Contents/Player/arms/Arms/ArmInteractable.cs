using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-06-05 : ArmInteractable
 *              - 상호작용 시 바닥의 무기 오브젝트 전체를 ArmsManager에 넘겨줌
 * ========================================================= */

public class ArmInteractable : Interactable
{
    private Collider myCollider;

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
    }

    public void InteractWithPlayer(GameObject player)
    {
        if (player == null) return;

        if (GetComponent<IArmsBase>() == null)
        {
            Debug.LogError($"🚨 [ArmInteractable] {name}에 IArmsBase를 구현한 팔 스크립트가 없습니다.");
            return;
        }

        ArmsManager armsMgr = player.GetComponentInChildren<ArmsManager>();
        
        if (armsMgr != null)
        {
            Debug.Log($"🎯 [ArmInteractable] 플레이어가 이 무기 획득 시도!");
            
            // 💡 이 무기 오브젝트 전체를 통째로 넘김
            armsMgr.EquipNewArm(this.gameObject);
            
            
        }
        else
        {
            Debug.LogError($"🚨 [ArmInteractable] {player.name}에서 ArmsManager를 찾을 수 없습니다.");
        }
    }
}
