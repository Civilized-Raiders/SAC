using System;
using Fusion.Addons.FSM;
using Unity.VisualScripting;
using UnityEngine;

public class StatusEffectBehaviour : EnemyBehaviour
{
    public float stunTime = 10f;
    private float _duration = 3f;
    private EnemyStatusEffect _statusEffect;

    public void SetStatusEffect(EnemyStatusEffect statusEffect)
    {
        _statusEffect = statusEffect;
    }

    private void EnterStatusEffect()
    {
        switch (_statusEffect)
        {
            case EnemyStatusEffect.None:
                Debug.LogWarning("StatusEffect is None");
                break;
            case EnemyStatusEffect.Stun:
                // _owner.animator.SetTrigger(EnemyStringToHash.Stun);
                _duration = stunTime;
                break;
        }
    }
    protected override void OnEnterState()
    {
        EnterStatusEffect();
    }
    protected override void OnFixedUpdate()
    {

    }
    protected override void OnRender()
    {

    }
    protected override void OnExitState()
    {
    }

    protected override bool CanExitState(StateBehaviour nextState)
    {
        return _duration <= Machine.StateTime;
    }
}
