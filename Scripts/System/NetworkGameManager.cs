using System;
using Fusion;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Network game state shared across the gameplay scene.
/// </summary>
public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance;

    [Networked] public int CurrentMoney { get; set; }
    [Networked] public int RemainingTimeSeconds { get; set; }
    [Networked] public NetworkBool IsGameEnded { get; set; }
    [Networked] public NetworkBool IsGameSuccess { get; set; }
    [Networked] public NetworkBool OnClearTriggered { get; set; }
    [Networked] public NetworkBool IsTimerPaused { get; set; }

    public int targetMoney = 1000;
    public Action OnClear;
    public event Action<bool> OnGameEnded;

    [Header("Game Timer")]
    [Tooltip("제한 시간(초). 테스트 중에는 짧게 두고 실제 플레이 값으로 다시 조정하면 됩니다.")]
    [Min(1)]
    [SerializeField] private int gameDurationSeconds = 30;

    private ChangeDetector changeDetector;
    private float timerAccumulator;

    public bool IsCleared => CurrentMoney >= targetMoney;
    public int GameDurationSeconds => Mathf.Max(1, gameDurationSeconds);
    public int ElapsedTimeSeconds => Mathf.Clamp(GameDurationSeconds - RemainingTimeSeconds, 0, GameDurationSeconds);
    public string CurrentTimeDisplay => FormatSeconds(RemainingTimeSeconds);

    public override void Spawned()
    {
        Instance = this;

        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            CurrentMoney = 0;
            RemainingTimeSeconds = GameDurationSeconds;
            IsGameEnded = false;
            IsGameSuccess = false;
            OnClearTriggered = false;
            IsTimerPaused = false;
            timerAccumulator = 0f;
        }

        Debug.Log("NetworkGameManager Spawned");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (IsGameEnded)
        {
            return;
        }

        if (IsTimerPaused)
        {
            return;
        }

        if (RemainingTimeSeconds <= 0)
        {
            TriggerGameEnd(false);
            return;
        }

        timerAccumulator += Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;
        while (timerAccumulator >= 1f && !IsGameEnded)
        {
            timerAccumulator -= 1f;
            RemainingTimeSeconds = Mathf.Max(0, RemainingTimeSeconds - 1);

            if (RemainingTimeSeconds <= 0)
            {
                TriggerGameEnd(false);
            }
        }

        if (!IsGameEnded)
        {
            EvaluatePlayerResolution();
        }
    }

    public override void Render()
    {
        if (changeDetector == null)
        {
            return;
        }

        foreach (var change in changeDetector.DetectChanges(this))
        {
            if (change == nameof(IsGameEnded) && IsGameEnded)
            {
                OnGameEnded?.Invoke(IsGameSuccess);
            }
        }
    }

    public void AddMoney(int amount)
    {
        if (!CanEditMoney())
        {
            return;
        }

        CurrentMoney += Mathf.Max(0, amount);
        TryOnClear();
    }

    public void ConsumeMoney(int amount)
    {
        TryConsumeMoney(amount);
    }

    public bool TryConsumeMoney(int amount)
    {
        if (!CanEditMoney())
        {
            return false;
        }

        amount = Mathf.Max(0, amount);
        if (amount == 0)
        {
            return true;
        }

        if (CurrentMoney < amount)
        {
            return false;
        }

        CurrentMoney -= amount;
        return true;
    }

    public void TriggerGameEnd()
    {
        TriggerGameEnd(IsCleared);
    }

    public void TriggerGameEnd(bool isSuccess)
    {
        if (!CanEditMoney())
        {
            return;
        }

        if (IsGameEnded)
        {
            return;
        }

        IsGameEnded = true;
        IsGameSuccess = isSuccess;
        RemainingTimeSeconds = Mathf.Max(0, RemainingTimeSeconds);
    }

    public void ResetGameState()
    {
        if (!CanEditMoney())
        {
            return;
        }

        CurrentMoney = 0;
        RemainingTimeSeconds = GameDurationSeconds;
        IsGameEnded = false;
        IsGameSuccess = false;
        OnClearTriggered = false;
        IsTimerPaused = false;
        timerAccumulator = 0f;
    }

    public void SetTimerPaused(bool isPaused)
    {
        if (!CanEditMoney())
        {
            return;
        }

        IsTimerPaused = isPaused;
        if (isPaused)
        {
            timerAccumulator = 0f;
        }
    }

    private void TryOnClear()
    {
        if (OnClearTriggered)
        {
            return;
        }

        if (!IsCleared)
        {
            return;
        }

        OnClearTriggered = true;
        OnClear?.Invoke();
    }

    private bool CanEditMoney()
    {
        return Object == null || !Object.IsValid || HasStateAuthority;
    }

    private void EvaluatePlayerResolution()
    {
        PlayerDeathHandler[] handlers = FindObjectsByType<PlayerDeathHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (handlers == null || handlers.Length == 0) return;

        bool hasAnyPlayer = false;
        bool hasAnyEscapedPlayer = false;
        bool hasUnresolvedPlayer = false;

        for (int i = 0; i < handlers.Length; i++)
        {
            PlayerDeathHandler handler = handlers[i];
            if (handler == null || handler.Object == null || !handler.Object.IsValid) continue;

            hasAnyPlayer = true;

            if (handler.HasEscaped)
            {
                hasAnyEscapedPlayer = true;
                continue;
            }

            // ★ 배터리 이벤트 누락 안전장치
            Battery battery = handler.GetComponent<Battery>();
            bool isBatteryDead = battery != null && battery.CurrentBatteryValue <= 0f;

            if (handler.IsDead || isBatteryDead)
            {
                if (!handler.IsDead && isBatteryDead)
                {
                    handler.Die();
                }
                continue;
            }

            hasUnresolvedPlayer = true;
        }

        // ★ 유효한 플레이어가 없으면 타이머에 맡기고 return
        if (!hasAnyPlayer) return;

        // ★ 살아있는 플레이어가 없고 전원 해결된 경우 게임 종료
        if (!hasUnresolvedPlayer)
        {
            TriggerGameEnd(hasAnyEscapedPlayer);
        }
    }

    private static string FormatSeconds(int totalSeconds)
    {
        int safeSeconds = Mathf.Max(0, totalSeconds);
        int minutes = safeSeconds / 60;
        int seconds = safeSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}
