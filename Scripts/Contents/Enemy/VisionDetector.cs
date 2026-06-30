using UnityEngine;
using System.Collections.Generic;
using Fusion;

public class VisionDetector : MonoBehaviour
{
    [Header("View Setting")]
    public float viewRadius = 10f;
    [Range(0, 360)]
    public float viewAngle = 90f;

    [Header("Layer")]
    public LayerMask targetLayer;
    public LayerMask obstacleLayer;

    
    public NetworkTransform FindVisibleTargets()
    {
        NetworkTransform currentTarget = null;
        var targetsInViewRadius = new Collider[10];
        
        var size = Physics.OverlapSphereNonAlloc(
            transform.position, viewRadius, results: targetsInViewRadius, targetLayer);
        float closestDistance = Mathf.Infinity;
        for (var i = 0; i < size; i++)
        {
            NetworkTransform target = null;
            if (targetsInViewRadius[i].TryGetComponent<NetworkTransform>(out var networkTransform))
                target = networkTransform;
            else
                continue;

            if (target.TryGetComponent<Battery>(out var battery))
            {
                if(battery.BatteryPercent <= 0)
                    continue;
            }
            else
                continue;
            var dirToTarget =
                (target.transform.position - transform.position).normalized;

            var distanceToTarget =
                Vector3.Distance(transform.position, target.transform.position);

            // 시야각 계산
            var angle =
                Vector3.Angle(transform.forward, dirToTarget);

            // 시야각 안에 있는지
            if (angle < viewAngle * 0.5f)
            {
                // 장애물 체크
                if (Physics.Raycast(
                        transform.position,
                        dirToTarget,
                        distanceToTarget,
                        obstacleLayer))
                    continue;

                // 가장 가까운 타겟 저장
                if (distanceToTarget < closestDistance)
                {
                    closestDistance = distanceToTarget;
                    currentTarget = target;
                }
            }
        }
        return currentTarget;
    }
}