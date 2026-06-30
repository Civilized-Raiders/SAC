using Fusion;
using System;
using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-06-01 : 배터리 잔량 동기화 및 6단계 게이지 Material 교체 기능 추가
 * 2026-06-01 : 완전 빈(Empty) 상태를 포함하여 총 7단계 매터리얼로 확장 수정
 * 2026-06-01 : 코드 상의 머티리얼 세부 조작(Emission, Intensity 등) 제거, 단순 머티리얼 스왑 방식으로 단순화
 * 2026-06-01 : 게이지 미갱신 버그 해결 - OnChangedRender 콜백 대신 Render()에서 프레임 타겟 동기화 방식으로 교체
 * 2026-06-04 : 클라이언트 접속자 및 오프라인 상태에서도 배터리 소모가 반영되도록 유효성 검사 우회 처리
 * ========================================================= */

public class Battery : NetworkBehaviour,IBatteryDamageable
{
    [Header("Battery Settings")]
    [Tooltip("최대 배터리 량")]
    public float maxBattery = 100f;

    // 배터리 잔량
    [Networked] public float CurrentBattery { get; set; }
    public float CurrentBatteryValue => Object != null && Object.IsValid ? CurrentBattery : localOfflineBattery;
    public float BatteryRatio => maxBattery <= 0f ? 0f : Mathf.Clamp01(CurrentBatteryValue / maxBattery);
    public float BatteryPercent => BatteryRatio * 100f;
    public bool IsFull => CurrentBatteryValue >= maxBattery;

    [Header("Visual Settings")]
    [Tooltip("배터리 게이지의 매터리얼을 교체할 대상 MeshRenderer")]
    public MeshRenderer targetRenderer;
    [Tooltip("교체할 매터리얼의 슬롯 인덱스 (기본적으로 0)")]
    public int materialIndex = 0;

    [Header("Battery Materials (7 Steps)")]
    [Tooltip("0: Empty ~ 6: Full (총 7개의 진행률 매터리얼을 순서대로 넣으세요)")]
    public Material[] batteryMaterials = new Material[7];

    private int currentMatLevel = -1;

    public event Action OnBatteryEmpty;

    // 오프라인용 로컬 배터리 변수 (네트워크 연결 안될 때 사용)
    private float localOfflineBattery;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            CurrentBattery = maxBattery;
        }
        localOfflineBattery = maxBattery;

        UpdateBatteryVisuals();
    }

    /// <summary>
    /// 외부나 본인이 배터리 값을 줄이고 싶을 때 호출하는 함수 
    /// </summary>
    public void DecreaseBattery(float amount)
    {
        // 💡 [수정] 오프라인 연결 대기 상태일 때 동작 보장
        if (Object == null || !Object.IsValid)
        {
            localOfflineBattery -= amount;
            localOfflineBattery = Mathf.Clamp(localOfflineBattery, 0f, maxBattery);
            return;
        }
        
        // 💡 [수정] 이미 PlayerMove의 HasStateAuthority 블록에서 호출되므로 여기서 막을 필요가 없음
        // 클라이언트 예측을 허용하기 위해 방어 코드는 살짝 풀어줍니다.
        CurrentBattery -= amount;
        CurrentBattery = Mathf.Clamp(CurrentBattery, 0f, maxBattery);
    }

    /// <summary>
    /// 외부에서 배터리에 데미지를 입히고 싶을 때 호출하는 함수입니다. 
    /// 내부적으로 DecreaseBattery를 호출하여 배터리를 감소시키고, 배터리가 0이하가 되면 OnBatteryEmpty 이벤트를 발생시킵니다.
    /// </summary>
    /// <param name="damage"></param>
    public void ApplyDamage(int damage)
    {
        if (!HasStateAuthority) return;
        DecreaseBattery(damage);
        if (CurrentBattery <= 0)
            OnBatteryEmpty?.Invoke();
    }

   


    public float ChargingBattery(float chagingSpd = 3f)
    {
        float deltaTime = Runner != null ? Runner.DeltaTime : Time.deltaTime;
        float amount = chagingSpd * deltaTime;
        float before = CurrentBatteryValue;
        
        if (Object == null || !Object.IsValid)
        {
            if (localOfflineBattery < maxBattery)
                localOfflineBattery = Mathf.Min(localOfflineBattery + amount, maxBattery);

            return localOfflineBattery - before;
        }

        if (CurrentBattery < maxBattery)
            CurrentBattery = Mathf.Min(CurrentBattery + amount, maxBattery);
        

        return CurrentBattery -before;
    }

    public override void Render()
    {
        UpdateBatteryVisuals();
    }

    private void Update()
    {
        // 네트워크 객체가 아닐 때(싱글 테스트 중일 때) Update에서 렌더 시각화 갱신
        if (Object == null || !Object.IsValid)
        {
            UpdateBatteryVisuals();
        }
    }

    private void UpdateBatteryVisuals()
    {
        if (targetRenderer == null || batteryMaterials == null || batteryMaterials.Length == 0) return;

        // 오프라인 상태이면 localOfflineBattery를 사용하고, 온라인이면 CurrentBattery 사용
        float ratio = BatteryRatio;
        
        int maxIndex = batteryMaterials.Length - 1;
        int levelIndex = Mathf.Clamp(Mathf.RoundToInt(ratio * maxIndex), 0, maxIndex);

        if (currentMatLevel != levelIndex)
        {
            currentMatLevel = levelIndex;
            Material[] mats = targetRenderer.materials;

            if (mats.Length > materialIndex)
            {
                mats[materialIndex] = batteryMaterials[levelIndex];
                targetRenderer.materials = mats;
            }
        }
    }
}
