using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-05-25 : Interactable 기본 클래스 작성
 *              - 상호작용 시 호출될 Interact() 메서드 추가
 * ========================================================= */

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    private void Start()
    {
        // 이 컴포넌트가 붙은 오브젝트의 콜라이더가 트리거로 설정되지 않았다면 경고를 출력합니다.
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[{gameObject.name}] Interactable 오브젝트의 콜라이더는 isTrigger로 설정되어 있어야 합니다.");
        }
    }

    /// <summary>
    /// 플레이어가 상호작용(E키)을 시도했을 때 호출되는 함수
    /// </summary>
    public virtual void Interact()
    {
        // 아이템 획득, 문 열기 등 다양한 오브젝트에서 이 메서드를 오버라이드하거나 확장하여 사용합니다.
        Debug.Log($"[{gameObject.name}] 상호작용");
    }
}