using Fusion;
using UnityEngine;

public class RailgunArm : NetworkBehaviour, IArmsBase
{
    private enum ProjectileForwardAxis
    {
        Z,
        Y,
        X
    }

    public string ArmName => "RailGun";
    public ArmType ArmType => ArmType.Gun;
    public bool IsEquipped { get; private set; }
    public bool IsUsing => isCharging;
    public float BatteryDrainRate => 0f;

    [Header("Visual Set")]
    [SerializeField] private GameObject[] visualModels;

    [Header("Physics")]
    [SerializeField] private Rigidbody itemRigidbody;

    [Header("Charge")]
    [SerializeField] private float chargeSeconds = 3f;
    [SerializeField] private float fireBatteryCost = 30f;

    [Header("Fire Hit Area")]
    [SerializeField] private GameObject railgunProjectilePrefab; //호스트판정
    [SerializeField] private GameObject railgunEffectPrefab; //발사이펙
    [SerializeField] private GameObject railgunChaging;//차징이펙
    [SerializeField] private ProjectileForwardAxis projectileForwardAxis = ProjectileForwardAxis.Y;
    [SerializeField] private float hitLength = 12f;
    [SerializeField] private float hitRadius = 0.35f;
    [SerializeField] private float hitLifeTime = 1f;
    [SerializeField] private LayerMask hitLayerMask = Physics.DefaultRaycastLayers;

    private GameObject ownerPlayer;
    private Battery ownerBattery;
    private Transform firePoint;
    private Transform directionTarget;
    private bool isCharging;
    private float chargeStartTime;

    [Header("ownerPlayer")]
    PlayerMove ownerPlayerMove;

    public void InjectFireContext(Transform firePoint, Transform directionTarget)
    {
        this.firePoint = firePoint;
        this.directionTarget = directionTarget;
    }
    public void Equip(GameObject owner)
    {
        ownerPlayer = owner;
        ownerBattery = ownerPlayer != null ? ownerPlayer.GetComponent<Battery>() : null;
        ownerPlayerMove = ownerPlayer !=null ? ownerPlayer.GetComponent<PlayerMove>() : null;

        IsEquipped = true;
        isCharging = false;

        SetWorldItemVisible(true);
        SetPhysics(false);
        enabled = true;
    }

    public void Unequip()
    {
        CancelCharge();

        IsEquipped = false;
        ownerBattery = null;
        ownerPlayer = null;
        ownerPlayerMove =null;

        SetWorldItemVisible(false);
        SetPhysics(true);
        enabled = false;
    }

    public void UseStart()
    {
        if (!IsEquipped) return;
        if (Object != null && Object.IsValid && !HasStateAuthority) return;
        if (!isCharging)
        {
            StartCharge();
            return;
        }

        if (GetChargeElapsedTime() < chargeSeconds)
        {
            CancelCharge();
            return;
        }

        Fire();
    }

    public void UseUpdate() { }

    public void UseEnd()
    {
        CancelCharge();
    }

    public void SetWorldItemVisible(bool visible)
    {
        if (visualModels == null) return;

        foreach (GameObject model in visualModels)
        {
            if (model != null) model.SetActive(visible);
        }
    }

    private void StartCharge()
    {
        isCharging = true;
        Rpc_SetChargingEffect(true);
        chargeStartTime = GetCurrentTime();
        Debug.Log("[RailgunArm] Charge Start");
    }

    private void CancelCharge()
    {
        if (!isCharging) return;

        isCharging = false;
        Rpc_SetChargingEffect(false);
        Debug.Log("[RailgunArm] Charge Cancel");
    }

    private void Fire()
    {
        if (!HasStateAuthority) return;


        isCharging = false;
        Rpc_SetChargingEffect(false);


        Transform startOrigin = firePoint != null ? firePoint : transform;
        Vector3 startPosition = startOrigin.position;

        if (directionTarget == null)
        {
            Debug.LogWarning("[RailgunArm] Railgun Target Point가 연결되지 않아 Fire Point forward 방향으로 발사합니다.");
        }

        Vector3 forward = directionTarget != null
            ? (directionTarget.position - startPosition).normalized
            : startOrigin.forward;

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = startOrigin.forward;
        }

        

        Debug.DrawRay(startPosition, forward * hitLength, Color.red, hitLifeTime);

        Quaternion rotation = GetProjectileRotation(forward);
        if (railgunProjectilePrefab == null)
        {
            Debug.Log("레일건판정프리팹연결미싱");
            return;
        }
        ConsumeFireBattery();
        Vector3 scale = GetProjectileScale();
        Rpc_PlayRailgunFireEffect(startPosition, rotation, scale);
        Rpc_PlayRailgunShotAudio();



        GameObject hitArea = Instantiate(railgunProjectilePrefab, startPosition, rotation);

        hitArea.name = "RailgunProjectile";
        hitArea.transform.SetPositionAndRotation(startPosition, rotation);
        hitArea.transform.localScale = scale;

        Collider[] colliders = hitArea.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            BoxCollider box = hitArea.AddComponent<BoxCollider>();
            
            box.size = new Vector3(0.5f, 5f, 0.5f);
            box.center = new Vector3(0f, 2f, 0f);
            colliders = new Collider[] { box };
        }

        foreach (Collider col in colliders)
        {
            if (col != null) col.isTrigger = true;
        }

        Rigidbody rb = hitArea.GetComponent<Rigidbody>();
        if (rb == null) rb = hitArea.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;


        
        RailgunHitArea railgunHitArea = hitArea.GetComponent<RailgunHitArea>();
        if (railgunHitArea == null) railgunHitArea = hitArea.AddComponent<RailgunHitArea>();
        railgunHitArea.Initialize(hitLayerMask);

        Destroy(hitArea, hitLifeTime);
        Debug.Log("[RailgunArm] Fire");
    }

    private Quaternion GetProjectileRotation(Vector3 forward)
    {
        Quaternion lookRotation = Quaternion.LookRotation(forward, Vector3.up);

        switch (projectileForwardAxis)
        {
            case ProjectileForwardAxis.Y:
                return lookRotation * Quaternion.Euler(90f, 0f, 0f);
            case ProjectileForwardAxis.X:
                return lookRotation * Quaternion.Euler(0f, -90f, 0f);
            default:
                return lookRotation;
        }
    }

    private Vector3 GetProjectileScale()
    {
        float diameter = hitRadius * 2f;
        float length = hitLength * 2f;

        switch (projectileForwardAxis)
        {
            case ProjectileForwardAxis.Y:
                return new Vector3(diameter, length, diameter);
            case ProjectileForwardAxis.X:
                return new Vector3(length, diameter, diameter);
            default:
                return new Vector3(diameter, diameter, length);
        }
    }

    private void ConsumeFireBattery()
    {
        if (fireBatteryCost <= 0f) return;

        if (ownerBattery == null && ownerPlayer != null)
        {
            ownerBattery = ownerPlayer.GetComponent<Battery>();
        }

        if (ownerBattery == null)
        {
            Debug.LogWarning("[RailgunArm] Owner Battery를 찾지 못했습니다.");
            return;
        }

        ownerBattery.DecreaseBattery(fireBatteryCost);
    }
    private float GetChargeElapsedTime()
    {
        return GetCurrentTime() - chargeStartTime;
    }

    private float GetCurrentTime()
    {
        return Runner != null ? (float)Runner.SimulationTime : Time.time;
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
        //itemRigidbody.useGravity = isOnGround;
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayRailgunFireEffect(Vector3 startPosition, Quaternion rotation, Vector3 scale)
    {
        if (railgunEffectPrefab == null) return;


        GameObject effect = Instantiate(railgunEffectPrefab, startPosition, rotation);
        effect.transform.localScale = scale;
        Destroy(effect, hitLifeTime);
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SetChargingEffect(bool active)
    {
        if (railgunChaging != null)
            railgunChaging.SetActive(active);
        if (active)
            PlayChargingAudio();
        else
            StopChargingAudio();
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayRailgunShotAudio()
    {
        StopChargingAudio();

        if (ownerPlayerMove == null) return;
        if (ownerPlayerMove.headAudioSource == null) return;
        if (ownerPlayerMove.railgunShutClip == null) return;

        ownerPlayerMove.headAudioSource.PlayOneShot(ownerPlayerMove.railgunShutClip);
    }

    private void PlayChargingAudio()
    {
        if (ownerPlayerMove == null) return;
        if (ownerPlayerMove.headAudioSource == null) return;
        if (ownerPlayerMove.railgunChargingClip == null) return;

        AudioSource source = ownerPlayerMove.headAudioSource;

        source.Stop();
        source.clip = ownerPlayerMove.railgunChargingClip;
        source.loop = false;
        source.time = 0f;
        source.Play();
    }

    private void StopChargingAudio()
    {
        if (ownerPlayerMove == null) return;
        if (ownerPlayerMove.headAudioSource == null) return;

        AudioSource source = ownerPlayerMove.headAudioSource;

        if (source.clip == ownerPlayerMove.railgunChargingClip)
        {
            source.Stop();
            source.clip = ownerPlayerMove.headRotateClip;
            source.loop = false;
        }
    }
}




