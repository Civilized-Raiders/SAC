using Fusion;
using UnityEngine;

public class ArmsManager : NetworkBehaviour
{
    [Header("Mount Point")]
    public Transform armMountPoint;

    [Header("Common Drop Point")]
    public Transform playerDropPoint;

    [Header("Carry Detect Trigger")]
    [Tooltip("카메라(또는 카메라 자식)에 붙은 isTrigger Collider를 연결하세요.")]
    public Collider carryDetectTrigger;

    [Header("Railgun")]
    [SerializeField] private Transform railgunFirePoint;
    [SerializeField] private Transform railgunTargetPoint;

    [Header("Battery Debug")]
    [SerializeField] private bool logArmBatteryDrain = true;

    [Header("Arm Drop Grounding")]
    [SerializeField] private LayerMask armDropGroundLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private float armDropRayHeight = 3f;
    [SerializeField] private float armDropRayDistance = 10f;
    [SerializeField] private float armGroundPadding = 0.05f;
    [SerializeField] private float armFallbackLift = 0.5f;
    [SerializeField] private float armDropSpawnHeight = 1f;
    [SerializeField] private float armDropUpwardVelocity = 0f;

    public IArmsBase CurrentArm { get; private set; }

    private Battery ownerBattery;
    private float nextBatteryLogTime;

    private void Awake()
    {
        if (armMountPoint == null)
        {
            armMountPoint = this.transform;
        }

        ownerBattery = transform.root.GetComponent<Battery>();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object != null && Object.IsValid && !HasStateAuthority) return;

        DrainCurrentArmBattery(Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime, "FixedUpdateNetwork");
    }

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;
        if (HasStateAuthority) return;

        NetworkObject ownerNetObj = transform.root.GetComponent<NetworkObject>();
        if (ownerNetObj == null || !ownerNetObj.HasInputAuthority) return;

        DrainCurrentArmBattery(Time.deltaTime, "UpdateLocal");
    }

    private void DrainCurrentArmBattery(float deltaTime, string source)
    {
        if (CurrentArm == null)
        {
            LogArmBatteryState(source, "No current arm");
            return;
        }

        if (!CurrentArm.IsUsing)
        {
            LogArmBatteryState(source, $"{CurrentArm.ArmName} not using");
            return;
        }

        if (ownerBattery == null)
        {
            ownerBattery = transform.root.GetComponent<Battery>();
        }

        if (ownerBattery == null)
        {
            LogArmBatteryState(source, $"{CurrentArm.ArmName} owner battery missing");
            return;
        }

        if (ownerBattery.CurrentBatteryValue <= 0f)
        {
            CurrentArm.UseEnd();
            LogArmBatteryState(source, $"{CurrentArm.ArmName} stopped because battery is empty");
            return;
        }

        if (CurrentArm.BatteryDrainRate <= 0f)
        {
            LogArmBatteryState(source, $"{CurrentArm.ArmName} drain rate is zero");
            return;
        }

        float beforeBattery = ownerBattery.CurrentBatteryValue;
        float amount = CurrentArm.BatteryDrainRate * deltaTime;
        ownerBattery.DecreaseBattery(amount);
        if (ownerBattery.CurrentBatteryValue <= 0f)
        {
            CurrentArm.UseEnd();
        }
        LogArmBatteryState(source, $"{CurrentArm.ArmName} draining {amount:F4}/tick, rate={CurrentArm.BatteryDrainRate:F2}, battery={beforeBattery:F2}->{ownerBattery.CurrentBatteryValue:F2}");
    }

    private void LogArmBatteryState(string source, string message)
    {
        if (!logArmBatteryDrain || Time.time < nextBatteryLogTime) return;

        nextBatteryLogTime = Time.time + 1f;
        Debug.Log($"[ArmsManager Battery] {source}: {message}");
    }

    public void EquipNewArm(GameObject newArmObj)
    {
        if (newArmObj == null) return;

        NetworkObject armNetObj = newArmObj.GetComponent<NetworkObject>();

        if (Object != null && Object.IsValid)
        {
            if (armNetObj == null || !armNetObj.IsValid)
            {
                Debug.LogWarning($"[ArmsManager] 네트워크 팔 오브젝트가 유효하지 않아 장착 요청을 중단합니다: {newArmObj.name}");
                return;
            }

            if (HasStateAuthority)
            {
                if (TryClaimArm(armNetObj))
                    Rpc_ExecuteEquipOnAll(armNetObj);
            }
            else
            {
                Rpc_RequestEquip(armNetObj);
            }

            return;
        }

        PerformEquip(newArmObj);
    }
    private bool TryClaimArm(NetworkObject armNetObj)
    {
        if (armNetObj == null)
            return false;

        ArmOwnership ownership = armNetObj.GetComponent<ArmOwnership>();
        if (ownership == null)
        {
            Debug.LogWarning($"[ArmsManager] ArmOwnership missing: {armNetObj.name}");
            return false;
        }

        NetworkObject ownerNetObj = transform.root.GetComponent<NetworkObject>();
        return ownership.TryClaim(ownerNetObj);
    }

    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    private void Rpc_RequestEquip(NetworkObject armNetObj)
    {
        if (!TryClaimArm(armNetObj))
            return;

        Rpc_ExecuteEquipOnAll(armNetObj);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ExecuteEquipOnAll(NetworkObject armNetObj)
    {
        if (armNetObj != null)
            PerformEquip(armNetObj.gameObject);
    }
    public void DetachCurrentArm()
    {
        if (CurrentArm == null) return;

        if (HasStateAuthority)
            Rpc_ExecuteDetachOnAll();
        else
            Rpc_RequestDetach();
    }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_RequestDetach()
    {
        Rpc_ExecuteDetachOnAll();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ExecuteDetachOnAll()
    {
        if (CurrentArm == null) return;
        Component armComp = (Component)CurrentArm;
        DropCurrentArm(armComp.gameObject, armComp.transform.position, armComp.transform.rotation);
        CurrentArm = null;
    }
    private void PerformEquip(GameObject newArmObj)
    {
        IArmsBase newArmLogic = newArmObj.GetComponent<IArmsBase>();
        if (newArmLogic == null)
        {
            Debug.LogError($"🚨 [ArmsManager] {newArmObj.name}에서 IArmsBase 스크립트를 찾을 수 없습니다.");
            return;
        }

        if (CurrentArm != null && ((Component)CurrentArm).gameObject == newArmObj)
        {
            return;
        }

        Vector3 dropPosition = newArmObj.transform.position + Vector3.up;
        Quaternion dropRotation = newArmObj.transform.rotation;

        SetArmGroundState(newArmObj, false);

        if (CurrentArm != null)
        {
            Component oldArmComp = (Component)CurrentArm;
            DropCurrentArm(oldArmComp.gameObject, dropPosition, dropRotation);
        }

        newArmObj.transform.SetParent(armMountPoint);
        newArmObj.transform.localPosition = Vector3.zero;
        newArmObj.transform.localRotation = Quaternion.identity;

        CurrentArm = newArmLogic;
        ownerBattery = transform.root.GetComponent<Battery>();

        if (CurrentArm is CarryArm carryArm)
        {
            carryArm.InjectDropPoint(playerDropPoint);
            carryArm.InjectCarryDetectTrigger(carryDetectTrigger); // 추가
        }

        if (CurrentArm is RailgunArm railgunArm)
        {
            railgunArm.InjectFireContext(railgunFirePoint, railgunTargetPoint);
        }

        CurrentArm.Equip(this.transform.root.gameObject);


        // ★ 팔 장착 시 플레이어 색 적용
        ApplyOwnerColorToArm(newArmObj);

    }
    private Transform GetOwnerCameraTransform()
    {
        Camera ownerCamera = transform.root.GetComponentInChildren<Camera>(true);
        return ownerCamera != null ? ownerCamera.transform : null;
    }
    private Vector3 GetSafeArmDropPosition(GameObject armObj, Vector3 basePosition)
    {
        Vector3 rayOrigin = basePosition + Vector3.up * armDropRayHeight;

        if (!TryFindDropGround(armObj, rayOrigin, out RaycastHit hit))
        {
            return basePosition + Vector3.up * armFallbackLift;
        }

        float bottomOffset = GetArmBottomOffset(armObj);
        return hit.point + Vector3.up * (bottomOffset + armGroundPadding);
    }

    private bool TryFindDropGround(GameObject armObj, Vector3 rayOrigin, out RaycastHit bestHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, armDropRayDistance, armDropGroundLayers, QueryTriggerInteraction.Ignore);
        bestHit = new RaycastHit();
        bool found = false;
        float bestDistance = 0f;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            if (armObj != null && hit.collider.transform.IsChildOf(armObj.transform)) continue;

            if (!found || hit.distance < bestDistance)
            {
                bestHit = hit;
                bestDistance = hit.distance;
                found = true;
            }
        }

        return found;
    }
    private float GetArmBottomOffset(GameObject armObj)
    {
        if (armObj == null) return armFallbackLift;

        bool foundBounds = false;
        float minY = 0f;

        Collider[] colliders = armObj.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col == null || col.isTrigger || !col.enabled) continue;

            float colliderMinY = col.bounds.min.y;
            if (!foundBounds || colliderMinY < minY)
            {
                minY = colliderMinY;
                foundBounds = true;
            }
        }

        if (!foundBounds)
        {
            Renderer[] renderers = armObj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled) continue;

                float rendererMinY = renderer.bounds.min.y;
                if (!foundBounds || rendererMinY < minY)
                {
                    minY = rendererMinY;
                    foundBounds = true;
                }
            }
        }

        if (!foundBounds) return armFallbackLift;

        return Mathf.Max(0f, armObj.transform.position.y - minY);
    }

    private void DropCurrentArm(GameObject oldArmObj, Vector3 dropPosition, Quaternion dropRotation)
    {
        if (oldArmObj == null || CurrentArm == null) return;

        IArmsBase oldArm = CurrentArm;

        NetworkObject ownerNetObj = transform.root.GetComponent<NetworkObject>();
        ArmOwnership ownership = oldArmObj.GetComponent<ArmOwnership>();
        if (ownership != null)
        {
            ownership.Release(ownerNetObj);
        }

        oldArm.Unequip();

        oldArmObj.transform.SetParent(null);
        Vector3 elevatedDropPosition = dropPosition + Vector3.up * armDropSpawnHeight;
        oldArmObj.transform.SetPositionAndRotation(elevatedDropPosition, dropRotation);

        SetArmPhysics(oldArmObj, false);
        SetArmColliders(oldArmObj, true);
        Physics.SyncTransforms();

        Vector3 safeDropPosition = GetSafeArmDropPosition(oldArmObj, elevatedDropPosition);
        //safeDropPosition.y += armDropSpawnHeight;
        oldArmObj.transform.SetPositionAndRotation(safeDropPosition, dropRotation);
        Physics.SyncTransforms();

        LiftArmAboveGround(oldArmObj);
        ResetArmRigidbody(oldArmObj);
        SetArmPhysics(oldArmObj, true);
        ApplyDropUpwardVelocity(oldArmObj);

        oldArm.SetWorldItemVisible(true);
    }
    private void SetArmGroundState(GameObject armObj, bool isOnGround)
    {
        SetArmColliders(armObj, isOnGround);
        SetArmPhysics(armObj, isOnGround);
    }

    private void SetArmColliders(GameObject armObj, bool enabled)
    {
        if (armObj == null) return;

        Collider[] colliders = armObj.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col != null) col.enabled = enabled;
        }
    }

    private void SetArmPhysics(GameObject armObj, bool isOnGround)
    {
        if (armObj == null) return;

        Rigidbody[] rigidbodies = armObj.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb == null) continue;

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.isKinematic = !isOnGround;
            
        }
    }

    private void LiftArmAboveGround(GameObject armObj)
    {
        if (armObj == null) return;

        Bounds bounds;
        if (!TryGetArmBounds(armObj, out bounds)) return;

        Vector3 rayOrigin = bounds.center + Vector3.up * armDropRayHeight;
        if (!TryFindDropGround(armObj, rayOrigin, out RaycastHit hit)) return;

        float penetration = hit.point.y + armGroundPadding - bounds.min.y;
        if (penetration <= 0f) return;

        armObj.transform.position += Vector3.up * penetration;
        Physics.SyncTransforms();
    }

    private bool TryGetArmBounds(GameObject armObj, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;

        Collider[] colliders = armObj.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col == null || col.isTrigger || !col.enabled) continue;

            if (!found)
            {
                bounds = col.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return found;
    }

    private void ResetArmRigidbody(GameObject armObj)
    {
        if (armObj == null) return;

        Rigidbody[] rigidbodies = armObj.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb == null || rb.isKinematic) continue;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void ApplyDropUpwardVelocity(GameObject armObj)
    {
        if (armObj == null || armDropUpwardVelocity <= 0f) return;

        Rigidbody[] rigidbodies = armObj.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb == null || rb.isKinematic) continue;

            Vector3 velocity = rb.linearVelocity;
            if (velocity.y < armDropUpwardVelocity)
            {
                velocity.y = armDropUpwardVelocity;
                rb.linearVelocity = velocity;
            }
        }
    }

    public void HandleFireInput(bool isPressed)
    {
        if (!isPressed || CurrentArm == null) return;

        if (!HasBatteryPower())
        {
            CurrentArm.UseEnd();
            return;
        }

        if (Object != null && Object.IsValid && !HasStateAuthority)
        {
            if (  CurrentArm is LightArm)
            {
                Rpc_RequestLightArmUseStart();
                return;
            }
            if(CurrentArm is RailgunArm)
            {
                Rpc_RequestRailgunUseStart();
                return;
            }
        }

        CurrentArm.UseStart();
    }



    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_RequestRailgunUseStart(RpcInfo info = default)
    {
        if (CurrentArm == null) return;
        if (!(CurrentArm is RailgunArm)) return;

        if (!HasBatteryPower())
        {
            CurrentArm.UseEnd();
            return;
        }

        CurrentArm.UseStart();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_RequestLightArmUseStart(RpcInfo info = default)
    {
        if (CurrentArm == null) return;
        if (!(CurrentArm is LightArm)) return;

        if (!HasBatteryPower())
        {
            CurrentArm.UseEnd();
            return;
        }

        CurrentArm.UseStart();
    }

    private bool HasBatteryPower()
    {
        if (ownerBattery == null)
        {
            ownerBattery = transform.root.GetComponent<Battery>();
        }

        return ownerBattery == null || ownerBattery.CurrentBatteryValue > 0f;
    }

    #region Color Apply

    private void ApplyOwnerColorToArm(GameObject armObj)
    {
        if (armObj == null) return;

        PlayerColor playerColor = transform.root.GetComponent<PlayerColor>();
        if (playerColor == null) return;

        Color color = playerColor.GetColor();
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();

        Renderer[] renderers = armObj.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;

            Material[] mats = r.sharedMaterials;

            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];

                // PlayerColor 머티리얼만 적용
                if (mat == null || mat.name != "PlayerColor") continue;

                r.GetPropertyBlock(mpb, i);
                mpb.SetColor("_BaseColor", color);
                r.SetPropertyBlock(mpb, i);
            }
        }
    }



    #endregion

}
