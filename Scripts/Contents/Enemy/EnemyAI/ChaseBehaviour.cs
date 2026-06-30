using Fusion;
using Fusion.Addons.FSM;
using UnityEngine;

public class ChaseBehaviour : EnemyBehaviour
{
    public float chaseDistance = 10f;
    [ReadOnly] public bool hasLostTarget;
    private Battery _targetBattery;
    protected override void OnEnterState()
    {
        _targetBattery = _owner.chaseTarget.GetComponent<Battery>();
    }

    protected override void OnFixedUpdate()
    {
        if (_targetBattery.BatteryPercent <= 0)
        {
            LostTarget();
            return;
        }
        var dis = Vector2.Distance(transform.position.XZ(), _owner.chaseTarget.transform.position.XZ());
        if (chaseDistance <= dis)
        {
            LostTarget();
        }

        void LostTarget()
        {
            _owner.chaseTarget = null;
            hasLostTarget = true;
        }
    }
    

    protected override bool CanExitState(StateBehaviour nextState)
    {
        return _owner.chaseTarget.IsNull();
    }
}
