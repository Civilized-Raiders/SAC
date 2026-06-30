using Fusion;
using UnityEngine;

public class EnemyCombat : NetworkBehaviour, IEnemyDamageable
{
    [Header("ScriptableObject")] 
    [SerializeField] private EnemyData _data;

    [Header("Inspector")] 
    private EnemyAI _enemyAI;

    [Header("Status")]
    [ReadOnly] public int maxHp = 1;
    [ReadOnly] public int currentHp = 1;
    [ReadOnly] public float moveSpeed;
    [ReadOnly] public int attackDamage;
    public bool IsDead => currentHp <= 0;

    public void Awake()
    {
        _enemyAI = GetComponent<EnemyAI>();
    }

    public override void Spawned()
    {
        SetData();
    }
    private void SetData()
    {
        maxHp = _data.maxHp;
        moveSpeed = _data.moveSpeed;
        attackDamage = _data.attackDamage;
        
        currentHp = maxHp;
        _enemyAI.navMeshAgent.speed = moveSpeed;
    }
    public void TakeDamage(int damage, EnemyStatusEffect statusEffect)
    {
        if (false == Object.HasStateAuthority)
            return;
        currentHp -= damage;
        _enemyAI.TryForceActivateState(statusEffect);
    }
}
