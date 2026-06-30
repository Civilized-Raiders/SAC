using Fusion;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


/* =========================================================
 * [Modification History]
 * 2026-06-15/16 : 호스트만 픽업/드롭 동기화되는 문제점 --> Client 동기화 완료
 * ========================================================= */
public class CarryArm : NetworkBehaviour, IArmsBase
{
    public string ArmName => "Carry Arm (기본 집게)";
    public ArmType ArmType => ArmType.Carry;
    public bool IsEquipped { get; private set; } = false;
    public bool IsUsing { get; protected set; }
    public float BatteryDrainRate => batteryDrainRate;

    [Header("Visual Set")]
    [SerializeField] private GameObject[] visualModels;

    [Header("Physics (Optional)")]
    [SerializeField] private Rigidbody itemRigidbody;

    [Header("Battery")]
    [SerializeField] private float batteryDrainRate = 0.5f;

    private const float MinDropPointWorldY = 0f;

    private Transform dropPoint;
    private Transform dropPointParent;
    private Vector3 initialDropPointLocalPos;
    protected Transform DropPoint => dropPoint;

    // 추가: 카메라 트리거 콜라이더로 캐리 타겟 탐지
    private Collider carryDetectTrigger;
    protected Collider CarryDetectTrigger => carryDetectTrigger;
    private Carryable currentTarget;
    private Carryable carriedItem;
    private GameObject ownerPlayer;

    public bool IsCarrying => carriedItem != null;
    protected Carryable CurrentTarget => currentTarget;

    private Carryable outlinedTarget;

    public void DropCarriedItemIfAny()
    {
        if (carriedItem == null) return;

        TryDrop();
    }

    public void InjectDropPoint(Transform playerDropTarget)
    {
        dropPoint = playerDropTarget;
        if (dropPoint != null)
        {
            dropPointParent = dropPoint.parent;
            initialDropPointLocalPos = dropPoint.localPosition;
        }
    }

    // 추가
    public void InjectCarryDetectTrigger(Collider trigger)
    {
        carryDetectTrigger = trigger;

        //if (carryDetectTrigger == null) return;  릴레이 붙이는부분 주석처리

        //CarryDetectTriggerRelay relay =
        //    carryDetectTrigger.GetComponent<CarryDetectTriggerRelay>();

        //if (relay == null)
        //    relay = carryDetectTrigger.gameObject.AddComponent<CarryDetectTriggerRelay>();

        //relay.Inject(this);
    }
    

    public void SetWorldItemVisible(bool visible)
    {
        if (visualModels == null) return;

        foreach (var mod in visualModels)
        {
            if (mod != null) mod.SetActive(visible);
        }
    }

    public virtual void Equip(GameObject owner)
    {
        ownerPlayer = owner;
        IsEquipped = true;

        SetWorldItemVisible(true);

        if (itemRigidbody != null)
        {
            itemRigidbody.isKinematic = true;
            itemRigidbody.useGravity = false;
        }

        this.enabled = true;
    }

    public virtual void Unequip()
    {
        if (carriedItem != null) TryDrop();

        if (IsLocalPlayer)
            ClearOutline();

        IsEquipped = false;
        ownerPlayer = null;
        currentTarget = null;

        SetWorldItemVisible(false);

        if (itemRigidbody != null)
        {
            itemRigidbody.isKinematic = false;
            itemRigidbody.useGravity = true;
        }

        this.enabled = false;
    }

    public virtual void UseStart()
    {
        //Debug.Log($"[CarryArm] UseStart CALLED frame={Time.frameCount}, time={Time.time:F3}, carried={(carriedItem != null ? carriedItem.name : "null")}, outlined={(outlinedTarget != null ? outlinedTarget.name : "null")}");
        if (!IsEquipped) return;



        if (carriedItem == null) 
        {
            currentTarget = outlinedTarget;
            TryPickUpCurrentTarget();
        } 
        else TryDrop();

        IsUsing = carriedItem != null;
        //Debug.Log($"[CarryArm] UseStart ENTER carriedItem={(carriedItem != null ? carriedItem.name : "null")}, currentTarget={(currentTarget != null ? currentTarget.name : "null")}, IsUsing={IsUsing}");
    }

    private void Update()
    {
        if (!IsEquipped) return;
        if (!IsLocalPlayer) return;
        if (carriedItem != null)
        {
            if (outlinedTarget != carriedItem)
            {
                if (outlinedTarget != null)
                    HideTargetFeedback(outlinedTarget);

                outlinedTarget = carriedItem;
                ShowTargetFeedback(outlinedTarget);
            }

            return;
        }
        UpdateOutlineTargetFromCamera();
        //UpdateOutlineOnly();
    }
    private void UpdateOutlineTargetFromCamera()
    {
        Carryable best = FindBestCarryableFromCamera();

        if (outlinedTarget == best) return;

        if (outlinedTarget != null)
            HideTargetFeedback(outlinedTarget);

        outlinedTarget = best;

        if (outlinedTarget != null)
            ShowTargetFeedback(outlinedTarget);



    }
    private void ShowTargetFeedback(Carryable target)
    {
        if (target == null) return;

        OutlineTool.Show(target.gameObject, OutlineTool.OutlineColorType.Yellow);

        SalvagePriceHandler priceHandler = target.GetComponentInParent<SalvagePriceHandler>();
        if (priceHandler != null)
            priceHandler.SetPriceVisible(true);
    }
    private void HideTargetFeedback(Carryable target)
    {
        if (target == null) return;

        OutlineTool.Hide(target.gameObject);

        SalvagePriceHandler priceHandler = target.GetComponentInParent<SalvagePriceHandler>();
        if (priceHandler != null)
            priceHandler.SetPriceVisible(false);
    }
    private Carryable FindBestCarryableFromCamera()
    {
        Carryable best = null;
        float bestDist = float.MaxValue;

        CapsuleCollider capsule = carryDetectTrigger as CapsuleCollider;
        if (capsule == null) return null;

        Transform t = capsule.transform;
        Vector3 center = t.TransformPoint(capsule.center);

        float radius = capsule.radius * Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y));
        float height = Mathf.Max(capsule.height * Mathf.Abs(t.lossyScale.z), radius * 2f);

        Vector3 axis = t.forward;
        float half = Mathf.Max(0f, height * 0.5f - radius);

        Vector3 p1 = center + axis * half;
        Vector3 p2 = center - axis * half;

        Physics.SyncTransforms();

        Collider[] hits = Physics.OverlapCapsule(
            p1,
            p2,
            radius,
            ~0,
            QueryTriggerInteraction.Collide);

        Transform ownerRoot = ownerPlayer != null ? ownerPlayer.transform.root : transform.root;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];

            if (hit.transform.root == ownerRoot)
                continue;

            Carryable c = hit.GetComponentInParent<Carryable>();
            if (c == null) continue;
            if (!c.CanBeCarryTarget) continue;

            float d = (c.transform.position - center).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }

        return best;
    }
    //구함수
    //private void UpdateOutlineOnly()
    //{
    //    if (outlinedTarget == currentTarget) return;

    //    if (outlinedTarget != null)
    //        OutlineTool.Hide(outlinedTarget.gameObject);

    //    outlinedTarget = currentTarget;

    //    if (outlinedTarget != null)
    //        OutlineTool.Show(outlinedTarget.gameObject, OutlineTool.OutlineColorType.Yellow);
    //}
    private void ClearOutline()
    {
        if (outlinedTarget != null)
        {
            HideTargetFeedback(outlinedTarget);
            outlinedTarget = null;
        }
    }
    public void UseUpdate() { }
    public virtual void UseEnd()
    {
       
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsEquipped) return;

       // UpdateTargetFromCameraTrigger();
        AdjustPositionsToAvoidClipping();
    }

    public Transform GetCarryPoint()
    {
        return dropPoint != null ? dropPoint : transform;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawCarryDetectCapsuleGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        DrawCarryDetectCapsuleGizmo();
    }

    private void DrawCarryDetectCapsuleGizmo()
    {
        CapsuleCollider capsule = GetGizmoCapsuleCollider();
        if (capsule == null) return;

        Transform t = capsule.transform;
        Vector3 center = t.TransformPoint(capsule.center);

        float radius = capsule.radius * Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y));
        float height = Mathf.Max(capsule.height * Mathf.Abs(t.lossyScale.z), radius * 2f);
        Vector3 axis = t.forward;
        float half = Mathf.Max(0f, height * 0.5f - radius);

        Vector3 p1 = center + axis * half;
        Vector3 p2 = center - axis * half;
        Vector3 right = t.right * radius;
        Vector3 up = t.up * radius;

        Gizmos.color = new Color(1f, 0.85f, 0f, 0.8f);
        Gizmos.DrawWireSphere(p1, radius);
        Gizmos.DrawWireSphere(p2, radius);
        Gizmos.DrawLine(p1 + right, p2 + right);
        Gizmos.DrawLine(p1 - right, p2 - right);
        Gizmos.DrawLine(p1 + up, p2 + up);
        Gizmos.DrawLine(p1 - up, p2 - up);

        Handles.color = new Color(1f, 0.85f, 0f, 1f);
        Handles.DrawWireDisc(p1, axis, radius);
        Handles.DrawWireDisc(p2, axis, radius);
    }

    private CapsuleCollider GetGizmoCapsuleCollider()
    {
        if (carryDetectTrigger is CapsuleCollider injectedCapsule)
            return injectedCapsule;

        CapsuleCollider[] childCapsules = GetComponentsInChildren<CapsuleCollider>(true);
        for (int i = 0; i < childCapsules.Length; i++)
        {
            CapsuleCollider childCapsule = childCapsules[i];
            if (childCapsule.isTrigger)
                return childCapsule;
        }

        return childCapsules.Length > 0 ? childCapsules[0] : null;
    }
#endif

    // 서버에서 픽업을 수행한 후 StateAuthority에서 CarryArm 상태를 갱신하기 위한 호출
    public void AuthoritativePickedUp(NetworkId itemId)
    {
        if (Runner == null) return;

        if (!Runner.TryFindObject(itemId, out NetworkObject obj))
            return;

        Carryable c = obj.GetComponent<Carryable>();

        if (c == null)
            return;

        carriedItem = c;
        currentTarget = null;
        IsUsing = true;
        //Debug.Log($"[CarryArm] AuthoritativePickedUp item={c.name}, before RefreshCarryPoint");

        c.SetLocalCarryPoint(GetCarryPoint());
    }
    

    // 서버에서 드롭을 수행한 후 StateAuthority에서 CarryArm 상태를 갱신하기 위한 호출
    public void AuthoritativeDropped(NetworkId itemId)
    {
        //Debug.Log($"AuthoritativeDropped CALLED : {itemId}");

        Carryable droppedItem = null;

        if (Runner != null &&
            Runner.TryFindObject(itemId, out NetworkObject obj))
        {
            droppedItem = obj.GetComponent<Carryable>();

            if (droppedItem != null)
            {
                droppedItem.ClearLocalCarryPoint();

                if (currentTarget == droppedItem)
                    currentTarget = null;
            }
        }

        bool droppedCurrentItem = carriedItem != null &&
            carriedItem.Object != null &&
            carriedItem.Object.Id == itemId;

        if (carriedItem == null || carriedItem == droppedItem || droppedCurrentItem)
        {
            carriedItem = null;
            IsUsing = false;
        }

       // Debug.Log($"carriedItem={carriedItem} IsUsing={IsUsing}");
    }
    //구함수
    //// 추가: 카메라 트리거 콜라이더(bounds) 기반 타겟 탐지
    //private void UpdateTargetFromCameraTrigger(bool updateOutline)
    //{
    //    if(carriedItem != null)
    //    {
    //        currentTarget = null;
    //        if (updateOutline) ClearOutline();
    //        return;
    //    } 

    //    {
    //        Carryable best = null;
    //        float bestDist = float.MaxValue;

    //        CapsuleCollider capsule = carryDetectTrigger as CapsuleCollider;
    //        if (capsule == null) return;

    //        Transform t = capsule.transform;
    //        Vector3 center = t.TransformPoint(capsule.center);

    //        float radius = capsule.radius * Mathf.Max(t.lossyScale.x, t.lossyScale.y);
    //        float height = Mathf.Max(capsule.height * t.lossyScale.z, radius * 2f);

    //        Vector3 axis = t.forward;
    //        float half = Mathf.Max(0f, height * 0.5f - radius);

    //        Vector3 p1 = center + axis * half;
    //        Vector3 p2 = center - axis * half;

    //        Collider[] hits = Physics.OverlapCapsule(
    //            p1,
    //            p2,
    //            radius,
    //            ~0,
    //            QueryTriggerInteraction.Collide);

    //        Transform ownerRoot = ownerPlayer != null ? ownerPlayer.transform.root : transform.root;

    //        for (int i = 0; i < hits.Length; i++)
    //        {
    //            Collider hit = hits[i];

    //            if (hit.transform.root == ownerRoot)
    //                continue;

    //            Carryable c = hit.GetComponentInParent<Carryable>();
    //            if (c == null) continue;

    //            float d = (c.transform.position - center).sqrMagnitude;
    //            if (d < bestDist)
    //            {
    //                bestDist = d;
    //                best = c;
    //            }
    //        }

    //        if (best != currentTarget)
    //        {
    //            currentTarget = best;
    //        }

    //        if (updateOutline)
    //        {
    //            UpdateOutlineOnly();
    //        }
    //    }
    //}

    private void AdjustPositionsToAvoidClipping()
    {
        if (dropPoint == null) return;

        Vector3 origin = transform.position + Vector3.up * 0.7f;
        int layerMask = LayerMask.GetMask("Ground", "Wall");

        Vector3 targetDrop = dropPointParent != null
            ? dropPointParent.position + dropPointParent.rotation * initialDropPointLocalPos
            : dropPoint.position;

        Vector3 dirDrop = targetDrop - origin;

        if (Physics.SphereCast(origin, 0.4f, dirDrop.normalized, out RaycastHit hitDrop, dirDrop.magnitude, layerMask, QueryTriggerInteraction.Ignore))
        {
            if (hitDrop.collider.transform.root != transform.root)
                dropPoint.position = hitDrop.point + hitDrop.normal * 0.4f;
            else
                dropPoint.localPosition = initialDropPointLocalPos;
        }
        else
        {
            dropPoint.localPosition = initialDropPointLocalPos;
        }

        if (Physics.Raycast(dropPoint.position + Vector3.up * 1f, Vector3.down, out RaycastHit groundHit, 2f, layerMask, QueryTriggerInteraction.Ignore))
        {
            if (dropPoint.position.y <= groundHit.point.y + 0.1f)
                dropPoint.position = new Vector3(dropPoint.position.x, groundHit.point.y + 0.3f, dropPoint.position.z);
        }
        ResolveCarriedItemOverlap(layerMask);
        //ClampDropPointMinY(); 일단 보류
    }
    private void ResolveCarriedItemOverlap(int layerMask)
    {
        if (carriedItem == null) return;

        Collider[] carriedColliders = carriedItem.CarryColliders;
        if (carriedColliders == null) return;

        for (int i = 0; i < carriedColliders.Length; i++)
        {
            Collider carriedCollider = carriedColliders[i];
            if (carriedCollider == null) continue;
            if (!carriedCollider.enabled) continue;

            Bounds bounds = carriedCollider.bounds;

            Collider[] blockingColliders = Physics.OverlapBox(
                bounds.center,
                bounds.extents,
                carriedCollider.transform.rotation,
                layerMask,
                QueryTriggerInteraction.Ignore);

            for (int j = 0; j < blockingColliders.Length; j++)
            {
                Collider blockingCollider = blockingColliders[j];
                if (blockingCollider == null) continue;
                if (blockingCollider.transform.root == carriedItem.transform.root) continue;

                if (Physics.ComputePenetration(
                    carriedCollider,
                    carriedCollider.transform.position,
                    carriedCollider.transform.rotation,
                    blockingCollider,
                    blockingCollider.transform.position,
                    blockingCollider.transform.rotation,
                    out Vector3 direction,
                    out float distance))
                {
                    dropPoint.position += direction * (distance + 0.02f);
                }
            }
        }
    }

    /*구코드 ClampDropPointMinY()
    private void ClampDropPointMinY()
    {
        if (dropPoint == null || dropPoint.position.y >= MinDropPointWorldY) return;

        Vector3 position = dropPoint.position;
        position.y = MinDropPointWorldY;
        dropPoint.position = position;
    }*/

    protected bool IsLocalPlayer
    {
        get
        {
            if (ownerPlayer == null) return false;
            NetworkObject ownerNetObj = ownerPlayer.GetComponent<NetworkObject>();
            return ownerNetObj != null && ownerNetObj.IsValid && ownerNetObj.HasInputAuthority;
        }
    }

    protected void TryPickUpCurrentTarget()
    {
        if (currentTarget == null) return;

       // Debug.Log($"[CarryArm] TryPickUpCurrentTarget: {currentTarget.name}");

        // 로컬 아웃라인 해제
        if (IsLocalPlayer) ClearOutline();

        NetworkObject carryNetObj = currentTarget.GetComponent<NetworkObject>();
        if (carryNetObj != null && carryNetObj.IsValid)
        {
            // 내가 StateAuthority(호스트)라면 즉시 처리
            if (carryNetObj.HasStateAuthority)
            {
                NetworkObject ownerNetObj = ownerPlayer != null
                ? ownerPlayer.GetComponent<NetworkObject>()
                : GetComponentInParent<NetworkObject>();
                
                AdjustPositionsToAvoidClipping();

                if (currentTarget.TryPickUp(GetCarryPoint(), ownerNetObj))
                {
                    carriedItem = currentTarget;
                    IsUsing = true;
                }
            }
            else
            {
                PlayerMove pm = GetComponentInParent<PlayerMove>();
                if (pm != null)
                {
                    // 로컬 예측 세팅을 하되, 재시뮬레이션에 의해 null이 되지 않도록 
                    // 아래 TryDrop 구조에서 방어 코드를 작성합니다.
                    pm.Rpc_RequestPickUpFromClient(carryNetObj.Id);
                }
            }
        }
        currentTarget = null;
        if (IsLocalPlayer) ClearOutline();
    }

    protected void TryPickUpTarget(Carryable target)
    {
        currentTarget = target;
        TryPickUpCurrentTarget();
    }

   

    private void TryDrop()
    {
        PlayerMove pm = GetComponentInParent<PlayerMove>();

        // 💡 퓨전 2 해결책: Invalid 속성 대신 구조체 초기값(default)을 대입합니다.
        NetworkId targetItemId = default;

        if (carriedItem != null && carriedItem.Object != null)
        {
            targetItemId = carriedItem.Object.Id;
        }
        // 💡 퓨전 2 해결책: 비교할 때도 무효값인지 체크하기 위해 .IsValid 속성을 검증합니다.
        else if (pm != null && pm.LastAuthoritativePickedUp.IsValid)
        {
            targetItemId = pm.LastAuthoritativePickedUp;
        }

        // 💡 최종 추출된 ID가 유효하지 않다면 드롭 중단
        if (!targetItemId.IsValid)
        {
            Debug.LogWarning("[CarryArm] 드롭할 아이템 타겟을 찾을 수 없습니다.");
            return;
        }

        if (IsLocalPlayer && pm != null)
        {
            //Debug.Log($"[Client -> Server] Drop RPC Request sent for ID: {targetItemId}");
            Carryable droppedItem = carriedItem;
            pm.Rpc_RequestDropFromClient(targetItemId);

            if (droppedItem != null)
                droppedItem.ForceLocalDropVisual();

            carriedItem = null;
            currentTarget = null;
            IsUsing = false;
            ClearOutline();
        }
        else if (Runner.IsServer)
        {
            if (carriedItem != null)
            {
                carriedItem.Drop(GetCarryPoint());
                carriedItem = null;
                currentTarget = null;
                IsUsing = false;
                ClearOutline();
            }
        }
    }

   
}

