using UnityEngine;

public class RailgunHitArea : MonoBehaviour ,IEnemyDamageable
{
    private LayerMask targetLayers;


    public void Initialize(LayerMask layers)
    {
        targetLayers = layers;
    }

    public void TakeDamage(int damage, EnemyStatusEffect statusEffect)
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        IEnemyDamageable damageable = other.GetComponentInParent<IEnemyDamageable>();
        if (damageable == null) return;

        damageable.TakeDamage(0, EnemyStatusEffect.Stun);

        Debug.Log($"[RailgunArm] Hit Target: {other.name}");
    }
}
