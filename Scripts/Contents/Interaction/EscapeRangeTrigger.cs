using UnityEngine;

/* =========================================================
 * EscapeTrigger 자식 오브젝트에 붙이는 컴포넌트
 * isTrigger 콜라이더의 Enter/Exit를 EscapePortal에 전달
 * ========================================================= */

public class EscapeRangeTrigger : MonoBehaviour
{
    private EscapePortal _portal;

    private void Awake()
    {
        _portal = GetComponentInParent<EscapePortal>();
        if (_portal == null)
            Debug.LogError("[EscapeRangeTrigger] 부모에서 EscapePortal을 찾을 수 없습니다.");
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerDeathHandler handler = other.GetComponentInParent<PlayerDeathHandler>();
        if (handler != null)
            _portal.OnPlayerEnterRange(handler);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerDeathHandler handler = other.GetComponentInParent<PlayerDeathHandler>();
        if (handler != null)
            _portal.OnPlayerExitRange(handler);
    }
}