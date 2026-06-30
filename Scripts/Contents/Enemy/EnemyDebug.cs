
using System;
using Fusion;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyDebug : NetworkBehaviour
{
#if UNITY_EDITOR
    [Header("Inspector")]
    [SerializeField] private EnemyAI enemyAI;
    [SerializeField] private VisionDetector visionDetector;
    
    [Header("Gizmos")]
    [SerializeField] private bool visionGizmos;
    [SerializeField] private bool chaseDisGizmos;
    [SerializeField] private bool attackPointGizmos;
    [SerializeField] private bool angleThreshold;

    [Header("Command")] 
    [SerializeField] private bool stunCommand;

    public override void FixedUpdateNetwork()
    {
        if (stunCommand)
        {
            enemyAI.TryForceActivateState(EnemyStatusEffect.Stun);
            stunCommand = false;
        }
    }

    private void OnDrawGizmos()
    {
        DrawGizmosVisionDetection();
        DrawGizmosChase();
        DrawGizmosAttackPoint();
        DrawGizmosAngleThreshold();
    }   
    private void DrawGizmosAngleThreshold()
    {
        if (false == angleThreshold)
            return;
        var position = transform.position;
        var attackState = enemyAI._attackState;
        var leftDir = Quaternion.Euler(0f, -attackState.angleThreshold, 0f) * transform.forward;
        var rightDir = Quaternion.Euler(0f, attackState.angleThreshold, 0f) * transform.forward;

        Gizmos.color = Color.red;

        Gizmos.DrawLine(
            position,
            position + leftDir * attackState.attackDis
        );

        Gizmos.DrawLine(
            position,
            position + rightDir * attackState.attackDis
        );
    }
    private void DrawGizmosAttackPoint()
    {
        if (false == attackPointGizmos)
            return;
        if (enemyAI._attackState.attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            enemyAI._attackState.attackPoint.position,
            enemyAI._attackState.attackRadius
        );
    }

    private void DrawGizmosChase()
    {
        if (false == chaseDisGizmos)
            return;
        Gizmos.color = Color.peru;
        Gizmos.DrawWireSphere(transform.position, enemyAI._chaseState.chaseDistance);
    }
    private void DrawGizmosVisionDetection()
    {
        if (false == visionGizmos)
            return;

        // 탐색 반경
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionDetector.viewRadius);

        // 시야각 선
        var leftBoundary = DirFromAngle(-visionDetector.viewAngle / 2);
        var rightBoundary = DirFromAngle(visionDetector.viewAngle / 2);

        Gizmos.color = Color.cyan;

        Gizmos.DrawLine(
            transform.position,
            transform.position + leftBoundary * visionDetector.viewRadius
        );

        Gizmos.DrawLine(
            transform.position,
            transform.position + rightBoundary * visionDetector.viewRadius
        );
        if (false == Application.isPlaying)
            return;
        // 감지된 타겟 표시
        if (enemyAI.chaseTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                transform.position,
                enemyAI.chaseTarget.transform.position
            );
        }

        // 방향 벡터 계산
        Vector3 DirFromAngle(float angleDegrees)
        {
            angleDegrees += transform.eulerAngles.y;

            return new Vector3(
                Mathf.Sin(angleDegrees * Mathf.Deg2Rad),
                0,
                Mathf.Cos(angleDegrees * Mathf.Deg2Rad)
            );
        }
    }
#endif

}
