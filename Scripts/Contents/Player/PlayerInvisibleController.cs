using Fusion;
using UnityEngine;

public class PlayerInvisibleController : NetworkBehaviour
{
    public static PlayerInvisibleController main;

    [Header("Layer")]
    [SerializeField] private string ownerHiddenLayerName = "Invisible";

    [Header("Owner Camera")]
    public Camera ownerCam;

    [Header("Hide From Owner Camera")]
    [SerializeField] private GameObject invisibleObj;

    public override void Spawned()
    {
        // 이 플레이어를 조종하는 클라이언트에서만 실행
        if (!HasInputAuthority)
            return;

        main = this;

        int invisibleLayer = LayerMask.NameToLayer(ownerHiddenLayerName);

        if (invisibleLayer < 0)
        {
            Debug.LogError($"{ownerHiddenLayerName} 레이어가 없습니다. Unity Layer 설정을 확인하세요.");
            return;
        }

        if (invisibleObj == null)
        {
            Debug.LogWarning("invisibleObj가 비어 있습니다.");
            return;
        }

        // 내 로컬 클라이언트에서만 선택한 오브젝트 하위 레이어 변경
        SetLayerRecursively(invisibleObj, invisibleLayer);

        // 내 카메라에서만 Invisible 레이어 제외
        if (ownerCam != null)
        {
            ownerCam.cullingMask &= ~(1 << invisibleLayer);
        }
        else
        {
            Debug.LogWarning("ownerCam이 비어 있습니다.");
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (main == this)
            main = null;
    }

    private void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;

        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}