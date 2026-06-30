using System.Collections.Generic;
using Fusion;
using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-06-12 : 정산 스테이션 작성
 *              - respawn 큐브 위에 올라온 폐품(SalvagePriceHandler)을 트리거로 감지
 *              - 정산 요청 시 가격 합산 → NetworkGameManager.AddMoney → 폐품 Despawn
 *              - 돈/삭제 처리는 Host(StateAuthority)에서만 수행 (ChargingStation 패턴)
 * 2026-06-14 : 잠금 타이밍 수정 (운반 중 허공 고정 버그 대응)
 *              - 폐품을 잡으면 isTrigger가 켜지는 변경과 충돌해, 운반 중 트리거 진입 즉시
 *                Carryable을 꺼버려 추적이 멈추고 허공에 고정되던 문제 수정.
 *              - 진입 시점이 아니라, 구역 안에서 실제로 내려놓아진 뒤(IsCarried==false)에만 잠그도록 변경.
 * ========================================================= */

[RequireComponent(typeof(Collider))]
public class SettlementStation : NetworkBehaviour
{
    [Header("Settlement Settings")]
    [Tooltip("정산 후 연속 입력으로 인한 중복 정산 방지용 쿨다운(초)")]
    [SerializeField] private float settleCooldown = 0.3f;

    // 정산 구역 안에 올라와 있는 폐품 목록 (물리 시뮬레이션 주체인 Host 기준으로만 신뢰)
    private readonly List<SalvagePriceHandler> itemsInZone = new List<SalvagePriceHandler>();

    private NetworkGameManager gameManager;
    private float lastSettleTime = -999f;

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[{name}] SettlementStation 콜라이더는 isTrigger가 켜져 있어야 합니다.");
        }
    }

    private void Update()
    {
        // 구역 안 폐품 중 "운반이 끝나(내려놓아져) 있는" 것만 집기 잠금 처리.
        // 운반 중(IsCarried==true)에 잠그면 추적 로직이 멈춰 허공에 고정되므로, 반드시 드롭 이후에만 잠근다.
        for (int i = 0; i < itemsInZone.Count; i++)
        {
            SalvagePriceHandler item = itemsInZone[i];
            if (item == null) continue;

            Carryable carryable = item.GetComponentInParent<Carryable>();
            if (carryable == null) continue;

            if (!carryable.IsCarried && carryable.enabled)
            {
                carryable.enabled = false;
                Debug.Log($"[SettlementStation] {item.name} 내려놓음 감지 → Carryable 잠금 (집기 차단).");
            }
        }
    }

    /// <summary>
    /// 버튼(SettlementButton)에서 호출. 어떤 피어에서 누르더라도 실제 정산은 Host(StateAuthority)에서 수행됩니다.
    /// </summary>
    public void RequestSettle()
    {
        bool networked = Object != null && Object.IsValid;
        Debug.Log($"[SettlementStation][진단] RequestSettle 호출됨. networked(스폰됨)={networked}, itemsInZone={itemsInZone.Count}");

        // 네트워크 환경: RPC로 Host에게 정산을 요청
        if (networked)
        {
            Rpc_Settle();
            return;
        }

        // 비네트워크(단독 테스트) 환경: 곧바로 정산
        Settle();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_Settle()
    {
        Debug.Log($"[SettlementStation][진단] Rpc_Settle 수신(StateAuthority). HasStateAuthority={HasStateAuthority}");
        Settle();
    }

    private void Settle()
    {
        Debug.Log($"[SettlementStation][진단] Settle 진입. HasStateAuthority={HasStateAuthority}, itemsInZone={itemsInZone.Count}");

        // Host에서만 돈/삭제 처리 (단, 비네트워크 단독 테스트에서는 통과)
        if (Object != null && Object.IsValid && !HasStateAuthority)
        {
            Debug.Log("[SettlementStation][진단] StateAuthority가 아니라서 중단 (이 피어는 정산 권한 없음).");
            return;
        }

        // 짧은 시간 내 중복 정산 방지
        float now = (Runner != null) ? (float)Runner.SimulationTime : Time.time;
        if (now - lastSettleTime < settleCooldown)
        {
            Debug.Log("[SettlementStation][진단] 쿨다운 중이라 중단.");
            return;
        }
        lastSettleTime = now;

        if (!ResolveGameManager()) return;

        Debug.Log($"[SettlementStation] 정산 전 현재 돈: {gameManager.CurrentMoney} (정산 대상 폐품 {itemsInZone.Count}개)");

        int total = 0;

        for (int i = itemsInZone.Count - 1; i >= 0; i--)
        {
            SalvagePriceHandler item = itemsInZone[i];
            itemsInZone.RemoveAt(i);

            // 이미 사라졌거나 유효하지 않은 항목은 건너뜀
            if (item == null) continue;

            total += Mathf.Max(0, item.CurrentPrice);

            if (item.Object != null && item.Object.IsValid && Runner != null)
            {
                Runner.Despawn(item.Object);
            }
            else
            {
                Destroy(item.gameObject);
            }
        }

        if (total > 0)
        {
            gameManager.AddMoney(total);
            Debug.Log($"[SettlementStation] 정산 완료: +{total}. 현재 돈: {gameManager.CurrentMoney}");
        }
        else
        {
            Debug.Log($"[SettlementStation] 정산할 폐품이 없습니다. 현재 돈: {gameManager.CurrentMoney}");
        }
    }

    private bool ResolveGameManager()
    {
        if (gameManager != null) return true;

        gameManager = NetworkGameManager.Instance;
        if (gameManager == null)
        {
            Debug.Log("[SettlementStation][진단] NetworkGameManager.Instance가 null → 정산 불가. (씬에 NetworkGameManager가 스폰/배치돼 있는지 확인 필요)");
            return false;
        }

        Debug.Log("[SettlementStation][진단] NetworkGameManager 연결 성공.");
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        SalvagePriceHandler item = other.GetComponentInParent<SalvagePriceHandler>();
        if (item == null)
        {
            Debug.Log($"[SettlementStation][진단] 트리거에 뭔가 들어옴: '{other.name}' → 하지만 SalvagePriceHandler가 없어 폐품으로 인식 안 함.");
            return;
        }

        if (!itemsInZone.Contains(item))
        {
            itemsInZone.Add(item);
            Debug.Log($"[SettlementStation] 폐품 감지: {item.name} (가격 {item.CurrentPrice})");

            // 잠금(Carryable 끄기)은 여기서 하지 않는다.
            // 운반 중인 폐품이 트리거에 닿자마자 꺼지면 추적이 멈춰 허공에 고정되므로,
            // 실제로 내려놓아진 뒤(Update에서 IsCarried==false 확인) 잠근다.
        }
    }

    private void OnTriggerExit(Collider other)
    {
        SalvagePriceHandler item = other.GetComponentInParent<SalvagePriceHandler>();
        if (item == null) return;

        if (itemsInZone.Remove(item))
        {
            Debug.Log($"[SettlementStation] 폐품 구역 이탈: {item.name}");

            // 구역을 벗어나면 다시 집을 수 있도록 Carryable 잠금 해제
            Carryable carryable = item.GetComponentInParent<Carryable>();
            if (carryable != null && !carryable.enabled)
            {
                carryable.enabled = true;
                Debug.Log($"[SettlementStation] {item.name}의 Carryable 재활성화 (집기 잠금 해제).");
            }
        }
    }
}
