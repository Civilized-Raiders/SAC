using Fusion;
using UnityEngine;

public class GameplayHUDBridge : MonoBehaviour
{
    [System.Serializable]
    private class ArmIconBinding
    {
        public string armScriptName;
        public Sprite icon;
    }

    [Header("UI")]
    [SerializeField] private GameplayHUDUI hudUI;

    [Header("Time")]
    [SerializeField] private bool useSceneElapsedTime = true;

    [Header("Arm Icon")]
    [SerializeField] private ArmIconBinding[] armIconBindings;

    private NetworkGameManager networkGameManager;
    private Battery localBattery;
    private ArmsManager localArmsManager;

    private int lastMoney = int.MinValue;
    private int lastTargetMoney = int.MinValue;
    private int lastCurrentBattery = int.MinValue;
    private int lastMaxBattery = int.MinValue;
    private string lastTimeText = string.Empty;
    private string lastArmScriptName = string.Empty;

    private void Awake()
    {
        if (hudUI == null)
        {
            hudUI = GetComponent<GameplayHUDUI>();
        }

        if (hudUI == null)
        {
            hudUI = GetComponentInChildren<GameplayHUDUI>(true);
        }
    }

    private void Update()
    {
        if (hudUI == null)
        {
            return;
        }

        TryResolveNetworkGameManager();
        TryResolveLocalBattery();
        TryResolveLocalArmsManager();

        UpdateMoney();
        UpdateTime();
        UpdateBattery();
        UpdateArmIcon();
    }

    private void TryResolveNetworkGameManager()
    {
        if (networkGameManager != null)
        {
            return;
        }

        networkGameManager = NetworkGameManager.Instance;
        if (networkGameManager != null)
        {
            return;
        }

        networkGameManager = FindFirstObjectByType<NetworkGameManager>();
    }

    private void TryResolveLocalBattery()
    {
        if (IsBatteryUsable(localBattery))
        {
            return;
        }

        Battery[] batteries = FindObjectsByType<Battery>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Battery battery in batteries)
        {
            if (battery == null)
            {
                continue;
            }

            if (battery.Object != null && battery.Object.IsValid)
            {
                if (battery.Object.HasInputAuthority)
                {
                    localBattery = battery;
                    return;
                }

                continue;
            }

            localBattery = battery;
            return;
        }
    }

    private void TryResolveLocalArmsManager()
    {
        if (IsArmsManagerUsable(localArmsManager))
        {
            return;
        }

        ArmsManager[] armsManagers = FindObjectsByType<ArmsManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ArmsManager armsManager in armsManagers)
        {
            if (armsManager == null)
            {
                continue;
            }

            NetworkObject ownerNetObj = armsManager.transform.root.GetComponent<NetworkObject>();
            if (ownerNetObj != null && ownerNetObj.IsValid)
            {
                if (ownerNetObj.HasInputAuthority)
                {
                    localArmsManager = armsManager;
                    return;
                }

                continue;
            }

            localArmsManager = armsManager;
            return;
        }
    }

    private void UpdateMoney()
    {
        if (!IsNetworkGameManagerUsable(networkGameManager))
        {
            return;
        }

        int currentMoney = networkGameManager.CurrentMoney;
        int targetMoney = Mathf.Max(0, networkGameManager.targetMoney);
        if (currentMoney == lastMoney && targetMoney == lastTargetMoney)
        {
            return;
        }

        lastMoney = currentMoney;
        lastTargetMoney = targetMoney;
        hudUI.SetMoney(currentMoney, targetMoney);
    }

    private void UpdateTime()
    {
        if (IsNetworkGameManagerUsable(networkGameManager))
        {
            string syncedTimeText = networkGameManager.CurrentTimeDisplay;
            if (syncedTimeText == lastTimeText)
            {
                return;
            }

            lastTimeText = syncedTimeText;
            hudUI.SetTime(syncedTimeText);
            return;
        }

        if (!useSceneElapsedTime)
        {
            return;
        }

        string timeText = FormatElapsedTime(Time.timeSinceLevelLoad);
        if (timeText == lastTimeText)
        {
            return;
        }

        lastTimeText = timeText;
        hudUI.SetTime(timeText);
    }

    private void UpdateBattery()
    {
        if (!IsBatteryUsable(localBattery))
        {
            return;
        }

        int currentBattery = Mathf.RoundToInt(localBattery.CurrentBatteryValue);
        int maxBattery = Mathf.RoundToInt(localBattery.maxBattery);

        if (currentBattery == lastCurrentBattery && maxBattery == lastMaxBattery)
        {
            return;
        }

        lastCurrentBattery = currentBattery;
        lastMaxBattery = maxBattery;
        hudUI.SetBattery(currentBattery, maxBattery, maxBattery);
    }

    private void UpdateArmIcon()
    {
        if (!IsArmsManagerUsable(localArmsManager))
        {
            if (!string.IsNullOrEmpty(lastArmScriptName))
            {
                lastArmScriptName = string.Empty;
                hudUI.ClearSpecialArm();
            }

            return;
        }

        IArmsBase currentArm = localArmsManager.CurrentArm;
        if (currentArm == null)
        {
            if (!string.IsNullOrEmpty(lastArmScriptName))
            {
                lastArmScriptName = string.Empty;
                hudUI.ClearSpecialArm();
            }

            return;
        }

        string armScriptName = currentArm.GetType().Name;
        if (armScriptName == lastArmScriptName)
        {
            return;
        }

        lastArmScriptName = armScriptName;
        hudUI.SetSpecialArm(FindArmIcon(armScriptName));
    }

    private Sprite FindArmIcon(string armScriptName)
    {
        if (armIconBindings == null)
        {
            return null;
        }

        foreach (ArmIconBinding binding in armIconBindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.armScriptName))
            {
                continue;
            }

            if (string.Equals(binding.armScriptName, armScriptName, System.StringComparison.Ordinal))
            {
                return binding.icon;
            }
        }

        return null;
    }

    private static bool IsBatteryUsable(Battery battery)
    {
        return battery != null && battery.isActiveAndEnabled;
    }

    private static bool IsNetworkGameManagerUsable(NetworkGameManager manager)
    {
        return manager != null && manager.Object != null && manager.Object.IsValid;
    }

    private static bool IsArmsManagerUsable(ArmsManager armsManager)
    {
        return armsManager != null && armsManager.isActiveAndEnabled;
    }

    private static string FormatElapsedTime(float elapsedSeconds)
    {
        int safeSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
        int minutes = safeSeconds / 60;
        int seconds = safeSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}
