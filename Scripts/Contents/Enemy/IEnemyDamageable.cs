using UnityEngine;

public interface IEnemyDamageable
{
    public void TakeDamage(int damage, EnemyStatusEffect statusEffect);
}

