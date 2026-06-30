using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using System.Collections.Generic;

/* =========================================================
 * [Modification History]
 * 2026-05-28 : IsCarried가 변경될 때 ChangeDetector를 통해 로컬 물리 상태(layer, isKinematic)를 클라이언트에서도 
 *              동일하게 처리하도록 수정.
 *              - NetworkRigidbody 보간이 정상적으로 동작하도록 로컬 시뮬레이션 제어.
 * 2026-06-05 : 팔을 따라갈 때 즉시 동기화가 아닌 부드럽게 딸려오도록 Lerp 적용
 * 2026-06-15&16 : 호스트만 픽업/드롭 동기화되는 문제점 --> 동기화 완료
 * ========================================================= */

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkRigidbody3D))]
public class Carryable : NetworkBehaviour
{
    private Collider[] colliders;


    private NetworkRigidbody3D netRb;
    private Rigidbody rb;

    [Networked] public NetworkBool IsCarried { get; set; }
    //
    [Networked] public NetworkId CarrierId { get; set; } //네트워크에저장될아이디
    private Transform currentCarryPoint;
    private Quaternion relativeRotation = Quaternion.identity; // 팔과 물건 사이의 초기 각도 차이

    // 부드럽게 끌려오는 속도 (수치가 작을수록 많이 뒤쳐져 따라옴, 15f 정도면 꽤 묵직함)
    [Header("Follow Settings")]
    public float followSpeed = 20f;
    public float rotationSpeed = 15f;

    [SerializeField] private float carryCollisionSkin = 0.03f;
    private float maxCarryMoveDistance = 0.15f;
    
    [SerializeField] private int maxPenetrationResolveIterations = 3;


    public static readonly List<Carryable> All = new List<Carryable>();

   

    private bool localCarryVisual;
    private bool suppressCarryVisualUntilServerDrop;

    public bool CanBeCarryTarget => !IsCarried || suppressCarryVisualUntilServerDrop;
    public Collider[] CarryColliders => colliders;


    private void Awake()
    {

        colliders = GetComponentsInChildren<Collider>();
        rb = GetComponent<Rigidbody>();
        netRb = GetComponent<NetworkRigidbody3D>();

        // Fusion NetworkRigidbody 보간 충돌 방지
        rb.interpolation = RigidbodyInterpolation.None;
    }
    public void RefreshCarryPoint()
    {
        currentCarryPoint = FindCarryPointByCarrierId();
        localCarryVisual = currentCarryPoint != null;
        if (localCarryVisual)
            suppressCarryVisualUntilServerDrop = false;
    }
    public void ClearLocalCarryPoint()
    {
        localCarryVisual = false;
        suppressCarryVisualUntilServerDrop = true;
        currentCarryPoint = null;
        ApplyDroppedLocalState();
    }
    public override void Spawned()
    {
        
        if (!All.Contains(this)) All.Add(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        All.Remove(this);
    }

    public override void Render()
    {
        if (!IsCarried)
            suppressCarryVisualUntilServerDrop = false;

        bool shouldCarry = !suppressCarryVisualUntilServerDrop && (IsCarried || localCarryVisual);

        if (shouldCarry)
        {
            ApplyCarriedLocalState();

            Transform foundPoint = FindCarryPointByCarrierId();
            if (foundPoint != null && currentCarryPoint != foundPoint)
            {
                currentCarryPoint = foundPoint;
                relativeRotation = Quaternion.Inverse(currentCarryPoint.rotation) * transform.rotation;
            }

            if (currentCarryPoint != null && !HasStateAuthority)
            {
                Vector3 targetPosition = currentCarryPoint.position;
                Quaternion targetRotation = currentCarryPoint.rotation * relativeRotation;

                transform.position = Vector3.Lerp(
                    transform.position,
                    targetPosition,
                    Time.deltaTime * followSpeed);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * rotationSpeed);
            }

            return;
        }

        ApplyDroppedLocalState();
    }
    private void ApplyCarriedLocalState()
    {
        gameObject.layer = LayerMask.NameToLayer("CarriedItem");

        if(colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].isTrigger = false;
            }
        }

        if (rb != null)
        {
            rb.detectCollisions = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (netRb != null &&HasStateAuthority)
            netRb.RBIsKinematic = false;
    }
    private void ApplyDroppedLocalState(bool syncPhysicsToVisual = false)
    {
        currentCarryPoint = null;

        if (syncPhysicsToVisual)
            SyncPhysicsToVisualTransform();

        gameObject.layer = LayerMask.NameToLayer("Default");

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].isTrigger = false;
            }
        }

        if (rb != null)
        {
            rb.detectCollisions = true;
            rb.useGravity = HasStateAuthority;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            if (!HasStateAuthority)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        if (netRb != null && HasStateAuthority)
            netRb.RBIsKinematic = false;
    }
    private void SyncPhysicsToVisualTransform()
    {
        if (rb != null)
        {
            rb.position = transform.position;
            rb.rotation = transform.rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
    }

    private Transform FindCarryPointByCarrierId()
    {
        if (Runner == null || !CarrierId.IsValid)
            return null;

        if (!Runner.TryFindObject(CarrierId, out NetworkObject carrierObj))
            return null;

        ArmsManager armsManager = carrierObj.GetComponentInChildren<ArmsManager>(true);
        if (armsManager == null)
            return null;

        if (armsManager.CurrentArm is CarryArm carryArm)
            return carryArm.GetCarryPoint();

        return null;
    }

    public bool TryPickUp(Transform carryPoint, NetworkObject carrierObject)
    {
        if (!HasStateAuthority) return false;
        if (carryPoint == null) return false;
        if (IsCarried) return false;
        if (CarrierId.IsValid) return false;

        CarrierId = carrierObject != null ? carrierObject.Id : default;
        IsCarried = true;

        currentCarryPoint = carryPoint;
        relativeRotation = Quaternion.Inverse(carryPoint.rotation) * transform.rotation;

        ApplyCarriedLocalState();

        //if (netRb != null)
        //    netRb.Teleport(carryPoint.position, carryPoint.rotation * relativeRotation);

        return true;
    }

    public void PickUp(Transform carryPoint, NetworkObject carrierObject)
    {
        TryPickUp(carryPoint, carrierObject);
    }

    public override void FixedUpdateNetwork()
    {

        // Host만 위치 계산
        if (!HasStateAuthority) return;
        if(IsCarried)
        {
            Transform foundPoint = FindCarryPointByCarrierId();
            if (foundPoint != null)
                currentCarryPoint = foundPoint;
        }

       
        if (IsCarried && currentCarryPoint != null)
        {
            // 💡 [수정] 즉시 덮어씌우는 대신, 현재 위치(위상)에서 목표 위치를 향해 부드럽게 선형 보간(Lerp)합니다.
            

            Vector3 targetPosition = currentCarryPoint.position;
            Quaternion targetRotation = currentCarryPoint.rotation * relativeRotation;

            Vector3 desiredPosition = Vector3.Lerp(
              rb.position,
              targetPosition,
              Runner.DeltaTime * followSpeed);

            desiredPosition = GetBlockedCarryPosition(rb.position, desiredPosition);

            Quaternion desiredRotation = Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                Runner.DeltaTime * rotationSpeed);

            desiredPosition = ResolveCarryPenetration(desiredPosition, desiredRotation);

            rb.MovePosition(desiredPosition);
            rb.MoveRotation(desiredRotation);
        }

    }
    private Vector3 ResolveCarryPenetration(Vector3 desiredPosition, Quaternion desiredRotation)
    {


        if (colliders == null || colliders.Length == 0)
            return desiredPosition;
        int carryBlockingLayers = LayerMask.GetMask("Ground", "Wall");
        if (carryBlockingLayers == 0)
            return desiredPosition;

        Vector3 resolvedPosition = desiredPosition;

        for (int iteration = 0; iteration < maxPenetrationResolveIterations; iteration++)
        {
            bool resolvedAny = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider carriedCollider = colliders[i];
                if (carriedCollider == null) continue;
                if (!carriedCollider.enabled) continue;
                if (carriedCollider.isTrigger) continue;

                Vector3 colliderPosition = GetPredictedColliderPosition(
                    carriedCollider,
                    resolvedPosition,
                    desiredRotation);

                Quaternion colliderRotation = GetPredictedColliderRotation(
                    carriedCollider,
                    desiredRotation);

                Bounds bounds = carriedCollider.bounds;

                Collider[] blockingColliders = Physics.OverlapBox(
                    colliderPosition,
                    bounds.extents,
                    colliderRotation,
                    carryBlockingLayers,
                    QueryTriggerInteraction.Ignore);

                for (int j = 0; j < blockingColliders.Length; j++)
                {
                    Collider blockingCollider = blockingColliders[j];
                    if (blockingCollider == null) continue;
                    if (blockingCollider.transform.root == transform.root) continue;

                    if (Physics.ComputePenetration(
                        carriedCollider,
                        colliderPosition,
                        colliderRotation,
                        blockingCollider,
                        blockingCollider.transform.position,
                        blockingCollider.transform.rotation,
                        out Vector3 direction,
                        out float distance))
                    {
                        resolvedPosition += direction * (distance + carryCollisionSkin);
                        resolvedAny = true;
                    }
                }
            }

            if (!resolvedAny)
                break;
        }

        return resolvedPosition;
    }
    private Vector3 GetPredictedColliderPosition(Collider targetCollider, Vector3 rootPosition, Quaternion rootRotation)
    {
        Vector3 localOffset = transform.InverseTransformPoint(targetCollider.transform.position);
        return rootPosition + rootRotation * localOffset;
    }

    private Quaternion GetPredictedColliderRotation(Collider targetCollider, Quaternion rootRotation)
    {
        Quaternion localRotation = Quaternion.Inverse(transform.rotation) * targetCollider.transform.rotation;
        return rootRotation * localRotation;
    }
    private Vector3 GetBlockedCarryPosition(Vector3 currentPosition, Vector3 desiredPosition)
    {
        Vector3 moveDelta = desiredPosition - currentPosition;
        float moveDistance = moveDelta.magnitude;

        if (moveDistance <= 0.001f)
            return desiredPosition;

        if (moveDistance > maxCarryMoveDistance)
        {
            moveDelta = moveDelta.normalized * maxCarryMoveDistance;
            moveDistance = maxCarryMoveDistance;
            desiredPosition = currentPosition + moveDelta;
        }

        Vector3 moveDirection = moveDelta / moveDistance;

        if (rb != null &&
            rb.SweepTest(
                moveDirection,
                out RaycastHit hit,
                moveDistance + carryCollisionSkin,
                QueryTriggerInteraction.Ignore))
        {
            return currentPosition + moveDirection * Mathf.Max(0f, hit.distance - carryCollisionSkin);
        }

        return desiredPosition;
    }

    public void SetLocalCarryPoint(Transform carryPoint)
    {
        currentCarryPoint = carryPoint;
        localCarryVisual = carryPoint != null;
        if (localCarryVisual)
        {
            relativeRotation = Quaternion.Inverse(carryPoint.rotation) * transform.rotation;
            suppressCarryVisualUntilServerDrop = false;
        }
    }

    public void ForceLocalDropVisual()
    {
        localCarryVisual = false;
        suppressCarryVisualUntilServerDrop = true;
        currentCarryPoint = null;
        ApplyDroppedLocalState();
    }

    public void Drop(Transform dropLocation)
    {
        //Debug.Log($"Carryable.Drop() ENTER : {name}");
        if (!HasStateAuthority) return;

        IsCarried = false;
        CarrierId = default;
        currentCarryPoint = null;
        localCarryVisual = false;
        suppressCarryVisualUntilServerDrop = false;

        Vector3 releasePosition = rb != null ? rb.position : transform.position;
        Quaternion releaseRotation = rb != null ? rb.rotation : transform.rotation;

        if (netRb != null)
        {
            netRb.RBIsKinematic = false;
            netRb.Teleport(releasePosition, releaseRotation);
        }
        else
        {
            transform.SetPositionAndRotation(releasePosition, releaseRotation);
        }

        if (rb != null)
        {
            rb.useGravity = true;
            rb.detectCollisions = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.linearVelocity = Vector3.up * 0.1f;
            rb.angularVelocity = Vector3.zero;
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].isTrigger = false;
            }
        }

        gameObject.layer = LayerMask.NameToLayer("Default");
    }
}
