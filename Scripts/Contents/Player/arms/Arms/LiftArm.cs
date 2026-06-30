using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class LiftArm : NetworkBehaviour, IArmsBase
{
    public string ArmName => "Lift Arm";
    public ArmType ArmType => ArmType.ForkLift;
    public bool IsEquipped { get; private set; }
    public bool IsUsing => isRaised;
    public float BatteryDrainRate => batteryDrainRate;

    [Header("Visual Set")]
    [SerializeField] private GameObject[] visualModels;

    [Header("Physics")]
    [SerializeField] private Rigidbody itemRigidbody;

    [Header("Lift")]
    [SerializeField] private Transform liftRoot;
    [SerializeField] private float loweredLocalY = 0f;
    [SerializeField] private float raisedLocalY = 0.6f;
    [SerializeField] private float liftSpeed = 4f;

    [Header("Lift Support")]
    [SerializeField] private bool moveSupportedBodies = true;
    [SerializeField] private Vector3 supportBoxCenter = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private Vector3 supportBoxSize = new Vector3(1f, 0.3f, 1f);
    [SerializeField] private LayerMask supportLayers = Physics.DefaultRaycastLayers;

    [Header("Battery")]
    [SerializeField] private float batteryDrainRate = 0.4f;

    private GameObject ownerPlayer;
    [Networked]private NetworkBool isRaised { get; set; }
    private readonly List<Rigidbody> supportedBodies = new List<Rigidbody>();

    public void Equip(GameObject owner)
    {
        ownerPlayer = owner;
        IsEquipped = true;
        

        SetWorldItemVisible(true);
        SetPhysics(false);
        SetLiftRootColliders(true);
        IgnoreOwnerCollision(true);
        enabled = true;
    }

    public void Unequip()
    {
        IgnoreOwnerCollision(false);

        IsEquipped = false;
        ownerPlayer = null;
        if(HasStateAuthority)
            isRaised = false;

        SetWorldItemVisible(false);
        SetPhysics(true);

        enabled = false;
    }
    private void IgnoreOwnerCollision(bool ignore)
    {
        if (ownerPlayer == null || liftRoot == null) return;

        Collider[] ownerColliders = ownerPlayer.GetComponentsInChildren<Collider>(true);
        Collider[] liftColliders = liftRoot.GetComponentsInChildren<Collider>(true);

        foreach (Collider ownerCol in ownerColliders)
        {
            if (ownerCol == null) continue;

            foreach (Collider liftCol in liftColliders)
            {
                if (liftCol == null) continue;
                Physics.IgnoreCollision(ownerCol, liftCol, ignore);
            }
        }
    }
    public override void FixedUpdateNetwork()
    {
        if (!IsEquipped || liftRoot == null) return;

        Vector3 beforePosition = liftRoot.position;

        float targetY = isRaised ? raisedLocalY : loweredLocalY;
        Vector3 localPos = liftRoot.localPosition;
        localPos.y = Mathf.MoveTowards(localPos.y, targetY, liftSpeed * Time.deltaTime);
        liftRoot.localPosition = localPos;

        Vector3 liftDelta = liftRoot.position - beforePosition;
        if(HasStateAuthority)
          MoveSupportedBodies(liftDelta);
    }

    public void UseStart()
    {
        if (!IsEquipped) return;

        if (HasStateAuthority)
        {
            isRaised = !isRaised;
        }
        else
        {
            RPC_RequestToggleLift();
        }
        
       
        Debug.Log($"[LiftArm] UseStart - IsRaised={isRaised}, IsUsing={IsUsing}, DrainRate={BatteryDrainRate}");
    }
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestToggleLift()
    {
        if (!IsEquipped) return;
        isRaised = !isRaised;
    }
    public void UseUpdate() { }

    public void UseEnd()
    {
        if (HasStateAuthority) isRaised = false;
        else RPC_RequestLowerLift();

       
    }
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestLowerLift()
    {
        if (!IsEquipped) return;
        isRaised = false;
    }
    public void SetWorldItemVisible(bool visible)
    {
        if (visualModels == null) return;

        foreach (GameObject model in visualModels)
        {
            if (model != null) model.SetActive(visible);
        }
    }

    private void SetPhysics(bool isOnGround)
    {
        if (itemRigidbody == null) return;

        if (!itemRigidbody.isKinematic)
        {
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }

        itemRigidbody.isKinematic = !isOnGround;
        itemRigidbody.useGravity = isOnGround;
    }

    private void SetLiftRootColliders(bool enabled)
    {
        if (liftRoot == null) return;

        Collider[] colliders = liftRoot.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col == null) continue;

            col.enabled = enabled;
            //if (enabled) col.isTrigger = false;
        }
    }

    private void MoveSupportedBodies(Vector3 liftDelta)
    {
        if (!moveSupportedBodies || liftRoot == null) return;
        if (liftDelta.sqrMagnitude <= 0.000001f) return;

        supportedBodies.Clear();

        Vector3 center = liftRoot.TransformPoint(supportBoxCenter);
        Vector3 halfExtents = supportBoxSize * 0.5f;
        Collider[] hits = Physics.OverlapBox(center, halfExtents, liftRoot.rotation, supportLayers, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            if (hit.transform.IsChildOf(transform.root)) continue;

            Rigidbody body = hit.attachedRigidbody;
            if (body == null || body.isKinematic) continue;
            if (supportedBodies.Contains(body)) continue;

            supportedBodies.Add(body);
            body.MovePosition(body.position + liftDelta);
        }
    }


}
