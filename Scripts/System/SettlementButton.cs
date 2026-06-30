using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-06-12 : 정산 버튼 작성
 *              - 플레이어가 E키로 상호작용 시 연결된 SettlementStation에 정산을 요청
 *              - Interaction.cs → Interactable.Interact() 파이프라인 재사용
 * 2026-06-12 : F키 전용 입력으로 변경
 *              - E(Interact) 파이프라인과 분리하기 위해 Interactable 상속 제거
 *              - 자체 트리거로 플레이어 근접을 감지하고, F키 입력 시 정산 요청
 *              - 정산은 단발 이벤트이며 RequestSettle() 내부에서 RPC로 Host에 전달됨
 * 2026-06-14 : 근접 판정을 물리 트리거 → 거리 계산으로 교체 (클라이언트 대응)
 *              - 클라이언트에선 네트워크 오브젝트의 OnTriggerEnter가 호스트 권위 물리 때문에 발화하지 않아
 *                playerInRange가 항상 false → F가 무시되던 문제 수정.
 *              - 로컬 플레이어(InputAuthority를 가진 Interaction)와의 거리로 근접을 판정하므로
 *                호스트/클라이언트 모두에서 동작. F 입력은 로컬에서 읽고 RequestSettle()이 RPC로 Host에 전달.
 * ========================================================= */

public class SettlementButton : MonoBehaviour
{
    [Header("Settlement")]
    [Tooltip("이 버튼이 정산을 요청할 대상 스테이션(respawn 큐브)")]
    [SerializeField] private SettlementStation station;

    [Tooltip("정산을 실행할 키")]
    [SerializeField] private KeyCode settleKey = KeyCode.F;

    [Tooltip("이 버튼으로부터 이 거리 이내에 내 플레이어가 있으면 정산 가능")]
    [SerializeField] private float settleRange = 3f;

    private Transform localPlayer;
    private bool wasInRange;

    private void Update()
    {
        bool inRange = IsLocalPlayerInRange();

        // 범위 진입/이탈 안내 로그 (트리거 대신 거리 기반)
        if (inRange && !wasInRange)
        {
            Debug.Log($"[SettlementButton] 정산 가능: {settleKey}키를 누르세요.");
        }
        else if (!inRange && wasInRange)
        {
            Debug.Log("[SettlementButton] 정산 범위를 벗어났습니다.");
        }
        wasInRange = inRange;

        if (!inRange) return;

        if (Input.GetKeyDown(settleKey))
        {
            Debug.Log($"[SettlementButton] {settleKey}키 눌림 → 정산 요청");

            if (station == null)
            {
                Debug.LogWarning($"[{name}] SettlementStation 참조가 비어 있습니다. 인스펙터에서 연결해 주세요.");
                return;
            }

            station.RequestSettle();
        }
    }

    private bool IsLocalPlayerInRange()
    {
        Transform player = GetLocalPlayer();
        if (player == null) return false;

        return Vector3.Distance(player.position, transform.position) <= settleRange;
    }

    /// <summary>
    /// 내 로컬 플레이어(InputAuthority를 가진 Interaction)를 찾아 캐싱한다.
    /// 물리 트리거에 의존하지 않으므로 클라이언트에서도 정상 동작한다.
    /// </summary>
    private Transform GetLocalPlayer()
    {
        if (localPlayer != null) return localPlayer;

        Interaction[] interactions = FindObjectsByType<Interaction>(FindObjectsSortMode.None);
        foreach (Interaction it in interactions)
        {
            if (it == null) continue;

            // 네트워크 환경: InputAuthority를 가진 것이 내 플레이어
            if (it.Object != null && it.Object.IsValid)
            {
                if (it.Object.HasInputAuthority)
                {
                    localPlayer = it.transform;
                    return localPlayer;
                }
            }
            else
            {
                // 비네트워크(단독 테스트): 첫 번째 Interaction을 로컬로 간주
                localPlayer = it.transform;
                return localPlayer;
            }
        }

        return null;
    }
}
