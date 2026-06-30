using ExitGames.Client.Photon.StructWrapping;
using Fusion;
using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-06-02 : 배터리 아이템 스크립트 작성 (W2 사전 준비)
 *              - GMP(이수현) : 충전 / 방전 / 손상 → 용량·가격 연동
 *              - 모든 가변 상태는 [Networked] 로 Host가 권한 보유, 전 클라 동기화
 *              - 장착/탈착은 BatterySlot 이 Charge/Drain 호출로 사용
 *              - Pickup 가능 여부는 Holder(들고 있는 주체) 유무로 판단
 * ========================================================= */

public class BatteryItem : NetworkBehaviour
{
    [Header("Battery Identity")]
    [Tooltip("이 배터리의 등급. 스펙은 BatteryData에서 조회한다.")]
    public BatteryTier tier = BatteryTier.Standard;

    // ===== 네트워크 동기 상태 (Host = StateAuthority가 계산) =====
    [Networked] public float Charge { get; set; }        // 현재 충전량
    [Networked] public float DamagePercent { get; set; } // 손상도 0~1 (1 = 완파에 가까움)
    [Networked] public NetworkBool IsEquipped { get; set; }

    // Holder : 현재 이 배터리를 들고 있는 플레이어(없으면 None → 바닥에 놓인 상태)
    [Networked] public PlayerRef Holder { get; set; }

    private BatterySpec spec;

    public override void Spawned()
    {
        spec = BatteryData.Get(tier);

        if (HasStateAuthority)
        {
            // 새로 스폰된 배터리는 손상 없는 만충 상태로 시작
            Charge = spec.maxCapacity;
            DamagePercent = 0f;
            IsEquipped = false;
            Holder = PlayerRef.None;
        }
    }

    /// <summary>손상을 반영한 실효 최대 용량. 손상이 클수록 담을 수 있는 양이 줄어든다.</summary>
    public float EffectiveMaxCapacity => spec.maxCapacity * (1f - DamagePercent);

    /// <summary>0~1 사이 잔량 비율. HUD(유호균)가 게이지 표시에 사용.</summary>
    public float ChargeRatio
    {
        get
        {
            float max = EffectiveMaxCapacity;
            return max <= 0f ? 0f : Mathf.Clamp01(Charge / max);
        }
    }

    public bool IsDead => Charge <= 0f;

    // ===== Host 전용 상태 변경 API =====

    /// <summary>장착 사용 중 방전. deltaTime 동안 기본 방전량만큼 깎는다.</summary>
    public void Drain(float deltaTime)
    {
        if (!HasStateAuthority) return;
        if (!IsEquipped) return;

        Charge = Mathf.Max(0f, Charge - spec.drainPerSecond * deltaTime);
    }

    /// <summary>충전 스테이션에서 충전. 손상으로 줄어든 실효 용량까지만 찬다.</summary>
    public void Charge_Tick(float deltaTime)
    {
        if (!HasStateAuthority) return;

        float target = EffectiveMaxCapacity;
        Charge = Mathf.Min(target, Charge + spec.chargeRate * deltaTime);
    }

    /// <summary>피격/충돌 등으로 손상 누적. 손상은 영구(이번 라운드 기준)이며 가격을 깎는다.</summary>
    public void ApplyDamage(float amount01)
    {
        if (!HasStateAuthority) return;

        DamagePercent = Mathf.Clamp01(DamagePercent + amount01);

        // 손상으로 실효 용량이 줄면 현재 충전량도 그 한도로 잘라준다.
        Charge = Mathf.Min(Charge, EffectiveMaxCapacity);
    }

    /// <summary>정산용 현재 가격. 손상도에 비례해 기본가에서 차감.</summary>
    public int GetCurrentPrice()
    {
        return Mathf.RoundToInt(spec.basePrice * (1f - DamagePercent));
    }

    // ===== Pickup / 장착 상태 전환 (BatterySlot, Pickup이 호출) =====

    public void SetHeldBy(PlayerRef player)
    {
        if (!HasStateAuthority) return;
        Holder = player;
        IsEquipped = false; // 손에 들면 장착 해제
    }

    public void ClearHolder()
    {
        if (!HasStateAuthority) return;
        Holder = PlayerRef.None;
    }

    public void SetEquipped(bool equipped)
    {
        if (!HasStateAuthority) return;
        IsEquipped = equipped;
        if (equipped) Holder = PlayerRef.None; // 슬롯에 꽂히면 손에서 떠남
    }
}
