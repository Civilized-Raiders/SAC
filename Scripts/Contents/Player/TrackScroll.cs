using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-05-26 : TrackScroll 작성
 *              - 무한궤도 텍스처(UV) 오프셋 스크롤 기능
 *              - URP(Lit) 셰이더 호환성 지원 (_BaseMap)
 * 2026-05-26 : 배열을 기반으로 다수의 머테리얼을 지정하여 스크롤하는 기능 추가
 * 2026-06-05 : Update의 자동 스크롤 제거, PlayerMove에서 탱크 조향 속도를 받아오도록 Scroll() 메서드 개방
 * ========================================================= */

[System.Serializable]
public class ScrollTarget
{
    [Tooltip("스크롤할 머테리얼의 인덱스 번호 (예: Element 1 이면 1)")]
    public int materialIndex = 0;
    
    [Tooltip("해당 머테리얼의 스크롤 속도 배율")]
    public float scrollSpeed = 5f;
    
    [Tooltip("해당 머테리얼의 스크롤 방향")]
    public Vector2 direction = new Vector2(0f, 1f);
    
    // 내부적으로 사용되는 변수들
    [HideInInspector] public Material mat;
    [HideInInspector] public string textureProperty = "";
    [HideInInspector] public Vector2 offset;
}

public class TrackScroll : MonoBehaviour
{
    [Tooltip("궤도의 렌더러")]
    [SerializeField] private Renderer trackRenderer;
    
    [Tooltip("스크롤을 원하시는 다수의 머테리얼 세팅을 추가하세요")]
    [SerializeField] private ScrollTarget[] scrollTargets; 

    private void Awake()
    {
        if (trackRenderer == null)
            trackRenderer = GetComponent<Renderer>();

        if (trackRenderer != null && scrollTargets != null)
        {
            Material[] mats = trackRenderer.materials;
            
            foreach (var target in scrollTargets)
            {
                if (target.materialIndex >= 0 && target.materialIndex < mats.Length)
                {
                    target.mat = mats[target.materialIndex];
                    
                    if (target.mat.HasProperty("_BaseMap")) 
                        target.textureProperty = "_BaseMap";
                    else if (target.mat.HasProperty("_MainTex")) 
                        target.textureProperty = "_MainTex";
                }
                else
                {
                    Debug.LogWarning($"[TrackScroll] Renderer에 Element {target.materialIndex}번 머테리얼이 없습니다!");
                }
            }
        }
    }

    // 💡 [수정] 자체 업데이트를 없애고 외부(PlayerMove)에서 조향 속도와 프레임 시간을 직접 받아 스크롤합니다.
    public void Scroll(float externalSpeed, float deltaTime)
    {
        if (scrollTargets == null) return;

        foreach (var target in scrollTargets)
        {
            if (target.mat == null) continue;

            // PlayerMove가 계산한 바퀴 속도 * 머티리얼별 배율 * 방향 역산
            target.offset += target.direction * (target.scrollSpeed * externalSpeed * deltaTime);

            if (!string.IsNullOrEmpty(target.textureProperty))
            {
                target.mat.SetTextureOffset(target.textureProperty, target.offset);
            }
            else
            {
                target.mat.mainTextureOffset = target.offset; 
            }
        }
    }
}