using System.Collections.Generic;
using Fusion;
using Fusion.Addons.FSM;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(
    typeof(EnemyDebug),
    typeof(EnemyCombat))]
public class EnemyAI : NetworkBehaviour, IStateMachineOwner
{
    [Header("Behaviour Status")]
    public IdleBehaviour _idleState;
    public MoveBehaviour _moveState;
    public AttackBehaviour _attackState;
    public DeadBehaviour _deadState;
    public ChaseBehaviour _chaseState;
    public PatrolBehaviour _patrolState;
    public StatusEffectBehaviour _statusEffect;
    [Space]
    private StateMachine<StateBehaviour> _enemyBasicAI;
    private StateMachine<StateBehaviour> _enemyMovementAI;

    [Networked] public NetworkTransform chaseTarget { get; set; }
    [Header("Inspector")] 
    public Animator animator;
    [ReadOnly] public NavMeshAgent navMeshAgent;
    [ReadOnly] public EnemyCombat enemyCombat;

    public void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        enemyCombat = GetComponent<EnemyCombat>();
    }

    public override void Spawned()
    {
        navMeshAgent.Warp(transform.position);
        _idleState.SetEnemyBase(this);
        _moveState.SetEnemyBase(this);
        _attackState.SetEnemyBase(this);
        _deadState.SetEnemyBase(this);
        _chaseState.SetEnemyBase(this);
        _patrolState.SetEnemyBase(this);
        _statusEffect.SetEnemyBase(this);
    }

    void IStateMachineOwner.CollectStateMachines(List<IStateMachine> stateMachines)
    {
        _enemyBasicAI = new StateMachine<StateBehaviour>("Enemy Basic",
            _idleState,
            _moveState,
            _attackState,
            _statusEffect,
            _deadState);
        stateMachines.Add(_enemyBasicAI);
        
        _enemyMovementAI = new StateMachine<StateBehaviour>("Enemy Movement",
            _patrolState,
            _chaseState);
        stateMachines.Add(_enemyMovementAI);

        _moveState.AddTransition(_idleState, () =>
        {
            if (_chaseState.hasLostTarget)
            {
                _chaseState.hasLostTarget = false;
                return true;
            }
            if (false == _moveState.IsPatrolling)
                return false;
            return CheckTransition(0.1f);

        });
        _moveState.AddTransition(_attackState, () =>
        {
            if (false == _moveState.IsChasing)
                return false;
            return CheckTransition(_attackState.attackDis);
        });
        _enemyBasicAI.ForceActivateState<IdleBehaviour>();
        bool CheckTransition(float stopDis)
        {
            var dis = Vector2.Distance(transform.position.XZ(), _moveState._movePoint.XZ());
            return navMeshAgent.pathStatus == NavMeshPathStatus.PathComplete &&
                   Mathf.Abs(dis) < stopDis;
        }
    }

    public void TryForceActivateState(EnemyStatusEffect statusEffect)
    {
        if (enemyCombat.IsDead)
            return;
        switch (statusEffect)
        {
            case EnemyStatusEffect.None:
                break;
            case EnemyStatusEffect.Stun:
                _statusEffect.SetStatusEffect(statusEffect);
                _enemyBasicAI.ForceActivateState<StatusEffectBehaviour>();
                break;
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        if (enemyCombat.IsDead)
        { 
            _enemyBasicAI.ForceActivateState<DeadBehaviour>();
            return;
        }

        _enemyBasicAI.TryActivateState(_moveState);
        
        if (chaseTarget)
            _enemyMovementAI.TryActivateState(_chaseState);
        else
            _enemyMovementAI.TryActivateState(_patrolState);
    }
}
