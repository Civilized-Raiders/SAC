using Fusion;
using UnityEngine;

public class LightArm : NetworkBehaviour, IArmsBase
{
    public string ArmName => "Light Arm";
    public ArmType ArmType => ArmType.Flash;
    public bool IsEquipped { get; private set; }
    public bool IsUsing { get; private set; }
    public float BatteryDrainRate => batteryDrainRate;

    [Header("Visual Set")]
    [SerializeField] private GameObject[] visualModels;

    [Header("Physics")]
    [SerializeField] private Rigidbody itemRigidbody;

    [Header("Light")]
    [SerializeField] private Light[] lights;

    [Header("Battery")]
    [SerializeField] private float batteryDrainRate = 0.25f;

    private GameObject ownerPlayer;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(IsLightOn))
            {
                IsUsing = IsLightOn;
                SetLights(IsLightOn);
            }
        }
    }

    [Networked] public NetworkBool IsLightOn { get; set; }
    private ChangeDetector _changeDetector;
    public void Equip(GameObject owner)
    {
        ownerPlayer = owner;
        IsEquipped = true;
        IsUsing = false;

        SetWorldItemVisible(true);
        SetLights(false);
        SetPhysics(false);

        enabled = true;
    }

    public void Unequip()
    {
        SetLights(false);

        IsEquipped = false;
        IsUsing = false;
        ownerPlayer = null;

        SetWorldItemVisible(false);
        SetPhysics(true);

        enabled = false;
    }

    public void UseStart()
    {
        if (!IsEquipped) return;
        if (!HasStateAuthority) return;
        IsLightOn = !IsLightOn;
        IsUsing = IsLightOn;

        SetLights(IsUsing);
        Debug.Log($"[LightArm] UseStart - IsUsing={IsUsing}, DrainRate={BatteryDrainRate}");
    }

    public void UseUpdate() { }

    public void UseEnd()
    {
        if (HasStateAuthority) IsLightOn = false;
        IsUsing = false;
        SetLights(false);
    }

    public void SetWorldItemVisible(bool visible)
    {
        if (visualModels == null) return;

        foreach (GameObject model in visualModels)
        {
            if (model != null) model.SetActive(visible);
        }
    }

    private void SetLights(bool isOn)
    {
        if (lights == null) return;

        foreach (Light light in lights)
        {
            if (light != null) light.enabled = isOn;
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
        //itemRigidbody.useGravity = isOnGround;
    }
}
