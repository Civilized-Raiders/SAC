using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-06-02 : 배터리 시스템 공통 정의 작성 (W2 사전 준비)
 *              - 배터리 등급(기본/고용량) enum
 *              - 등급별 스펙(최대용량/충전속도/기본가격) 정의 테이블
 *              - GMP(이수현) 방전/충전/손상값 · META-CORE(유호균) HUD 가 공유
 * ========================================================= */

/// <summary>
/// 배터리 등급. Won't Have(개조/특수능력)는 제외하고
/// Must(기본 1종) + Should(고용량 2종)만 정의한다.
/// </summary>
public enum BatteryTier
{
    Standard = 0,   // Must Have : 기본 배터리
    HighCap1 = 1,   // Should    : 고용량 A
    HighCap2 = 2,   // Should    : 고용량 B
}

/// <summary>
/// 등급별 고정 스펙. 런타임에 바뀌지 않는 값만 둔다.
/// (방전/충전/손상 같은 가변 상태는 Battery 쪽 Networked 프로퍼티가 담당)
/// </summary>
[System.Serializable]
public struct BatterySpec
{
    public float maxCapacity;     // 만충 용량
    public float chargeRate;      // 초당 충전량 (충전 스테이션에서 사용)
    public float drainPerSecond;  // 장착 사용 중 초당 기본 방전량
    public int basePrice;         // 손상 0% 기준 가격 (정산 계산 기준값)

    public BatterySpec(float maxCap, float charge, float drain, int price)
    {
        maxCapacity = maxCap;
        chargeRate = charge;
        drainPerSecond = drain;
        basePrice = price;
    }
}

public static class BatteryData
{
    /// <summary>등급으로 스펙을 조회. 인덱스를 enum 캐스팅으로 맞춘다.</summary>
    public static BatterySpec Get(BatteryTier tier)
    {
        switch (tier)
        {
            case BatteryTier.HighCap1:
                return new BatterySpec(maxCap: 200f, charge: 25f, drain: 3.5f, price: 600);
            case BatteryTier.HighCap2:
                return new BatterySpec(maxCap: 320f, charge: 30f, drain: 5.0f, price: 1100);
            case BatteryTier.Standard:
            default:
                return new BatterySpec(maxCap: 100f, charge: 20f, drain: 2.0f, price: 250);
        }
    }
}
