using System;
using Fusion;
using UnityEngine;

public class PlayerDeathHandler : NetworkBehaviour
{
    [Networked] public NetworkBool IsDead { get; set; }
    [Networked] public NetworkBool HasEscaped { get; set; }
    [Networked] public int ResolvedAtSeconds { get; set; }

    public event Action OnDied;
    public event Action OnRevived;
    public event Action OnEscaped;

    private ChangeDetector _changeDetector;
    private Battery _battery;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _battery = GetComponent<Battery>();

        if (_battery != null)
        {
            _battery.OnBatteryEmpty += Die;
        }

        if (HasStateAuthority)
        {
            HasEscaped = false;
            ResolvedAtSeconds = -1;
        }
    }

    public void Die()
    {
        if (!HasStateAuthority || IsDead) return;

        IsDead = true;
        if (ResolvedAtSeconds < 0 && NetworkGameManager.Instance != null)
        {
            ResolvedAtSeconds = NetworkGameManager.Instance.ElapsedTimeSeconds;
        }
    }

    public void MarkEscaped()
    {
        if (!HasStateAuthority || IsDead || HasEscaped) return;

        HasEscaped = true;
        if (ResolvedAtSeconds < 0 && NetworkGameManager.Instance != null)
        {
            ResolvedAtSeconds = NetworkGameManager.Instance.ElapsedTimeSeconds;
        }
    }

    public void Revive(float restoreRatio = 0.2f)
    {
        if (!HasStateAuthority || !IsDead) return;

        IsDead = false;
        HasEscaped = false;
        ResolvedAtSeconds = -1;

        if (_battery != null)
        {
            _battery.CurrentBattery = _battery.maxBattery * restoreRatio;
        }
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(IsDead))
            {
                if (IsDead)
                {
                    OnDied?.Invoke();
                }
                else
                {
                    OnRevived?.Invoke();
                }
            }

            if (change == nameof(HasEscaped) && HasEscaped)
            {
                OnEscaped?.Invoke();
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_battery != null)
        {
            _battery.OnBatteryEmpty -= Die;
        }
    }
}
