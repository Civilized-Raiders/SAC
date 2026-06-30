using UnityEngine;

public class PatrolBehaviour : EnemyBehaviour
{
    [SerializeField] private VisionDetector _visionDetector;

    protected override void OnEnterState()
    {
    }
    protected override void OnFixedUpdate()
    {
        if (false == Object.HasStateAuthority)
            return;

        if (0 == Runner.Tick.Raw % 5)
            return;

        SetChaseTarget();
    }

    private void SetChaseTarget()
    {
        _owner.chaseTarget = _visionDetector.FindVisibleTargets();
    }

    protected override void OnExitState()
    {
        
    }
}
