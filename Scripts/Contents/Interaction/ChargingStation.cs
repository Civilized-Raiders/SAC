using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ChargingStation : NetworkBehaviour
{
    [Header("Charging Settings")]
    [SerializeField] private float chargeMultiplier = 1f;
    [SerializeField] private float costPerUnit = 0.5f;
    [SerializeField] private float chargeCostInterval = 0.5f;

    private Battery chargingTarget;
    private NetworkGameManager gameManager;
    private bool loggedMissingGameManager;
    private bool loggedNotEnoughMoney;
    private float chargeCostTimer;

    public Battery CurrentChargingTarget => chargingTarget;
    public float LastChargedAmount { get; private set; }
    public float CurrentTargetBattery => chargingTarget != null ? chargingTarget.CurrentBatteryValue : 0f;
    public float CurrentTargetBatteryRatio => chargingTarget != null ? chargingTarget.BatteryRatio : 0f;
    public float CurrentTargetBatteryPercent => chargingTarget != null ? chargingTarget.BatteryPercent : 0f;
    public bool HasChargingTarget => chargingTarget != null;

    [Networked] public float AccumulatedCost { get; set; }

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[{name}] ChargingStation collider should be a trigger.");
        }

        ResolveGameManager();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (chargingTarget == null) return;
        if (!ResolveGameManager()) return;

        int chargeCost = Mathf.CeilToInt(costPerUnit);
        bool isFree = chargeCost <= 0;

        if (chargingTarget.IsFull)
        {
            return;
        }

        if (!isFree && gameManager.CurrentMoney < chargeCost)
        {
            if (!loggedNotEnoughMoney)
            {
                Debug.Log("[ChargingStation] Not enough money to charge.");
                loggedNotEnoughMoney = true;
            }
            return;
        }

        loggedNotEnoughMoney = false;
        LastChargedAmount = chargingTarget.ChargingBattery(3f * chargeMultiplier);
        chargeCostTimer += Runner.DeltaTime;

        if (!isFree && chargeCostTimer >= chargeCostInterval)
        {
            if (gameManager.TryConsumeMoney(chargeCost))
            {
                AccumulatedCost += chargeCost;
                Debug.Log($"[ChargingStation] Charged cost {chargeCost}. Money left: {gameManager.CurrentMoney}");
            }

            chargeCostTimer = 0f;
        }
    }

    private bool ResolveGameManager()
    {
        if (gameManager != null) return true;

        gameManager = NetworkGameManager.Instance;
        if (gameManager != null)
        {
            loggedMissingGameManager = false;
            return true;
        }

        if (!loggedMissingGameManager)
        {
            Debug.LogWarning("[ChargingStation] NetworkGameManager.Instance is missing. Waiting to charge.");
            loggedMissingGameManager = true;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        Battery battery = other.GetComponentInParent<Battery>();
        if (battery == null) return;

        chargingTarget = battery;
        LastChargedAmount = 0f;
        chargeCostTimer = 0f;
        Debug.Log($"[ChargingStation] Charging started: {battery.CurrentBatteryValue}");
    }

    private void OnTriggerExit(Collider other)
    {
        Battery battery = other.GetComponentInParent<Battery>();
        if (battery == null || battery != chargingTarget) return;

        float currentBattery = chargingTarget.CurrentBatteryValue;
        chargingTarget = null;
        LastChargedAmount = 0f;
        chargeCostTimer = 0f;
        Debug.Log($"[ChargingStation] Charging ended. Current battery: {currentBattery}");
    }
}
