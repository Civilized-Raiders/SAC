using Fusion;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class MoveBehaviour : EnemyBehaviour
{
    private NavMeshPath _path;
    [Networked] public bool IsPatrolling { get; private set; } = true;
    [Networked] public float MoveSpeed { get; private set; }
    [HideInInspector] public Vector3 _movePoint;
    private readonly float _baseAnimationMoveSpeed = 1.5f;
    public float moveRadius = 10;
    public bool IsChasing => false == IsPatrolling;

    public void Awake()
    {
        _path = new NavMeshPath();
    }

    protected override void OnEnterState()
    {
        SetDestination();
        _owner.navMeshAgent.isStopped = false;
    }
    protected override void OnFixedUpdate()
    {
        MoveSpeed = _owner.navMeshAgent.velocity.magnitude;
        if (Runner.Tick % 5 == 0)
        {
            IsPatrolling = _owner.chaseTarget.IsUnityNull();
            if (IsChasing)
                SetMovePoint();
        }
    }

    protected override void OnRender()
    {
        _owner.animator.SetTrigger(EnemyStringToHash.Move);
        _owner.animator.SetFloat(
            EnemyStringToHash.MoveSpeed,
            MoveSpeed / _baseAnimationMoveSpeed);
    }
    private void SetDestination()
    {
        if (_owner.chaseTarget.IsUnityNull())
        {   // 패트롤
            IsPatrolling = true;
            SetMovePoint();
        }
        else
        {   // 추적
            IsPatrolling = false;
            SetMovePoint();
        }
    }

    private void SetMovePoint()
    {
        var tryCount = 0;
        var sourcePosition = new Vector3();
        while (true)
        {
            ++tryCount;
            if (10 <= tryCount)
            {
                Debug.LogWarning("시도 횟수 초과");
                _movePoint = transform.position;
                return;
            }

            if (IsPatrolling)
                sourcePosition = GetMovePos();
            else
                sourcePosition = _owner.chaseTarget.transform.position;

            if (NavMesh.SamplePosition(sourcePosition, out var hit, moveRadius, NavMesh.AllAreas))
            {
                if (_owner.navMeshAgent.CalculatePath(hit.position, _path) &&
                    _path.status == NavMeshPathStatus.PathComplete)
                    _owner.navMeshAgent.SetDestination(hit.position);
                else
                    continue;
                _movePoint = hit.position;
            }
            return;
        }
    }
    private Vector3 GetMovePos()
    {
        var x = Random.Range(-moveRadius, moveRadius);
        var z = Random.Range(-moveRadius, moveRadius);
        var pos = transform.position;
        return new Vector3(pos.x + x, -1, pos.z + z);
    }
    protected override void OnExitState()
    {
        IsPatrolling = true;
        _owner.navMeshAgent.isStopped = true;
        _owner.navMeshAgent.ResetPath();
    }
}
