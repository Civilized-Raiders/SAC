using Fusion.Addons.FSM;
using UnityEngine;

public class IdleBehaviour : EnemyBehaviour
{
    public float idleTime = 2f;
    
    protected override void OnExitState()
    {
        
    }
    protected override void OnFixedUpdate()
    {
        
    }
    protected override void OnEnterState()
    {
        
    }
    protected override void OnRender()
    {
        _owner.animator.SetTrigger(EnemyStringToHash.Idle);
    }
    protected override bool CanExitState(StateBehaviour nextState)
    {
        return Machine.StateTime > idleTime;
    }
}
