using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

/*
 *
 & Functions
 &  [Private]
 &  : SpawnLoop()          - 대기 후 부족한 적 수 만큼 ReserveSpawn 예약
 &  : ReserveSpawn()       - 딜레이 후 NavMesh 위에 적 스폰
 &  : WatchEnemy()         - 사망 감지 후 Despawn + 카운트 복구
 &  : GetNavMeshPosition() - 스폰 반경 내 Walkable NavMesh 유효 위치 탐색
 */

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private NetworkObject _enemyPrefab;        // Enemy_Test 프리팹
    [SerializeField] private Vector3 _spawnPos;           // 스폰 중심 위치 (아트팀이 인스펙터에서 지정)
    [SerializeField] private float _spawnRadius = 5f;   // 스폰 반경
    [SerializeField] private float _spawnTime = 5f;   // 스폰 최대 대기 시간
    [SerializeField] private float _spawnDelay = 10f;  // 게임 시작 후 첫 스폰까지 대기 시간

    [SerializeField] private int _keepMonsterCount = 3;         // 유지할 최대 적 수

    private int _monsterCount = 0;
    private int _reserveCount = 0;
    private int _walkableMask;  // Walkable Area 마스크 (Road, Background 등 다른 NavMesh 무시)

    public override void Spawned()
    {
        if (!Object.HasStateAuthority) return;

        _walkableMask = 1 << NavMesh.GetAreaFromName("Walkable");
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(_spawnDelay);

        while (true)
        {
            while ((_reserveCount + _monsterCount) < _keepMonsterCount)
            {
                _reserveCount++;
                StartCoroutine(ReserveSpawn());
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator ReserveSpawn()
    {
        yield return new WaitForSeconds(Random.Range(1f, _spawnTime));

        Vector3 spawnPos = GetNavMeshPosition();
        Debug.Log(spawnPos);
        NetworkObject enemy = Runner.Spawn(
            _enemyPrefab,
            spawnPos,
            Quaternion.identity
        );

        _reserveCount--;

        if (enemy == null) yield break;

        _monsterCount++;
        StartCoroutine(WatchEnemy(enemy));
    }

    private IEnumerator WatchEnemy(NetworkObject enemy)
    {
        var combat = enemy.GetComponent<EnemyCombat>();

        while (enemy != null && enemy.IsValid && !combat.IsDead)
        {
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(2f);

        if (enemy != null && enemy.IsValid)
            Runner.Despawn(enemy);

        _monsterCount--;
    }

    private Vector3 GetNavMeshPosition()
    {
        for (int i = 0; i < 30; i++)
        {
            Vector3 randDir = Random.insideUnitSphere * _spawnRadius;
            randDir.y = 0;
            Vector3 candidate = _spawnPos + randDir;

            // Walkable만 탐색 + Y축 체크로 다른 층 NavMesh 방지
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, _walkableMask))
            {
                if (Mathf.Abs(hit.position.y - _spawnPos.y) <= 1f)
                    return hit.position;
            }
        }

        Debug.LogWarning("[EnemySpawner] NavMesh 유효 위치를 찾지 못했습니다. _spawnPos를 확인하세요.");
        return _spawnPos;
    }
}