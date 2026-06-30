using Fusion;
using UnityEngine;

/* =========================================================
 * [PlayerColor]
 * 2026-06-01 : 로비에서 고른 색을 입히고 전 클라이언트에 동기화.
 *  - Host가 Spawned 시점에 로비 PlayerData(ColorId)를 읽어 Networked ColorId에 기록.
 *  - 모든 머신은 Spawned 1회 + Render(ChangeDetector)로 색 적용 → late-join 포함 안전.
 *  - MaterialPropertyBlock 사용(머티리얼 인스턴스 복제 방지, 드로우콜 절약).
 * ========================================================= */
public class PlayerColor : NetworkBehaviour
{
    [Tooltip("색을 칠할 메쉬들 (탱크 바디 등). 궤도/바퀴 등 색 안 바꿀 건 제외")]
    [SerializeField] private Renderer[] bodyRenderers;

    [Tooltip("ColorId(int) → 실제 Color. 로비 색 인덱스 순서와 반드시 일치시킬 것")]
    [SerializeField] private Color[] palette;

    [Tooltip("URP Lit = _BaseColor, Built-in Standard = _Color")]
    [SerializeField] private string colorProperty = "_BaseColor";

    [Networked] public int ColorId { get; set; }

    private ChangeDetector _cd;
    private MaterialPropertyBlock _mpb;

    public override void Spawned()
    {
        _cd = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _mpb = new MaterialPropertyBlock();

        // Host만 로비 데이터에서 색을 읽어와 Networked 값으로 박는다.
        if (HasStateAuthority)
        {
            PlayerData data =
                FusionRoomManager.Instance != null
                    ? FusionRoomManager.Instance.GetPlayerData(Object.InputAuthority) : null;

            ColorId = (data != null && data.ColorId >= 0) ? data.ColorId : 0;
        }

        ApplyColor(); // late-join 포함 즉시 1회 적용
    }

    public override void Render()
    {
        if (_cd == null) return;

        foreach (var change in _cd.DetectChanges(this))
        {
            if (change == nameof(ColorId))
                ApplyColor();
        }
    }

    void ApplyColor()
    {
        if (palette == null || bodyRenderers == null) return;
        if (ColorId < 0 || ColorId >= palette.Length) return;

        foreach (Renderer r in bodyRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(colorProperty, palette[ColorId]);
            r.SetPropertyBlock(_mpb);
        }
    }

    public Color GetColor()
    {
        if (palette == null || ColorId < 0 || ColorId >= palette.Length)
            return Color.white;

        return palette[ColorId];
    }

}
