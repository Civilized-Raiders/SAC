using System;
using Fusion;
using Fusion.Addons.FSM;
using UnityEngine;

public class AttackBehaviour : EnemyBehaviour
{ 
    [Networked] private bool _isAttacked { get; set; }
    public Transform attackPoint;
    public float attackRadius = 1;
    public float attackDis = 3f;
    public float RotateSpeed = 100f;
    public float angleThreshold = 5f;
    protected override void OnEnterState()
    {
    }
    protected override void OnFixedUpdate()
    {
        if (_isAttacked)
            return;
        
        var pos = _owner.chaseTarget.transform.position;
        if (false == TargetInAngleRange(pos))
        {
            RotateToTarget(pos);
        }
    }
    protected override void OnRender()
    {
        if (_owner.chaseTarget == null)
            return;
        
        if (TargetInAngleRange(_owner.chaseTarget.transform.position))
        {
            _owner.animator.ResetTrigger(EnemyStringToHash.Move);
            _owner.animator.SetTrigger(EnemyStringToHash.Attack);
            _isAttacked = true;
        }
        else
        {
            if (_isAttacked)
                return;   
            _owner.animator.SetTrigger(EnemyStringToHash.Idle);
        }
    }
    protected override void OnExitState()
    {
        _isAttacked = false;
    }
    private void RotateToTarget(Vector3 targetPosition)
    {
        var targetRotation = GetTargetRotation(targetPosition);
 
        _owner.transform.rotation = Quaternion.RotateTowards(
            _owner.transform.rotation,
            targetRotation,
            RotateSpeed * Runner.DeltaTime
        );
    }

    private bool TargetInAngleRange(Vector3 targetPosition)
    {
        var targetRotation = GetTargetRotation(targetPosition);
        return Quaternion.Angle(
            _owner.transform.rotation,
            targetRotation
        ) <= angleThreshold;
    }
    
    private Quaternion GetTargetRotation(Vector3 targetPosition)
    {
        var dir = targetPosition - _owner.transform.position;
        dir.y = 0f;
        return Quaternion.LookRotation(dir);
    }
    
    public void OnAttack()
    {
        if (false == HasStateAuthority)
            return;
        var results = new Collider[5];
        var size = Physics.OverlapSphereNonAlloc(attackPoint.position, attackRadius, results);
        for (var i = 0; i < size; i++)
        {
            if (results[i].TryGetComponent<IBatteryDamageable>(out var iBatteryDamageable))
            {
                iBatteryDamageable.ApplyDamage(_owner.enemyCombat.attackDamage);
            }
        }
    }
    protected override bool CanExitState(StateBehaviour nextState)
    {
        if (false == _isAttacked)
            return false;

        return 1 <= _owner.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
    }
}
