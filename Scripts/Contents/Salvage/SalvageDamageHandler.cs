using System;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using Random = UnityEngine.Random;

public class SalvageDamageHandler : NetworkBehaviour, ISalvageSetData
{
    private NetworkRigidbody3D _rigidbody;
    [Networked, Capacity(HitCapacity)] private NetworkArray<int> _hitDamage { get; } = new();
    [Networked] private int HitCount { get; set; }
    private const int HitCapacity = 5;
    private int _previousHitCount;
    private Vector3 _prevVelocity;
    private float _condition = 100f; // 상태(100 = 완전한 상태)
    private float _weight = 1f;        // 회수품 무게
    private float _hardness = 5f;      // 회수품의 방어력
    private float _damageLimit = 50;   // 해당 값보다 낮은 데미지는 무시
    public event Action<int> OnDamaged;

    public void Set(SalvageData data)
    {
        _weight = data.weight;
        _hardness = data.hardness;
        _damageLimit = data.hardness;
    }
    public override void Spawned()
    {
        _rigidbody = GetComponent<NetworkRigidbody3D>();
        HitCount = 0;
    }
    
    public override void Render() 
    {
        if (_previousHitCount == HitCount)
            return;
        for (var i = 0; i < HitCount - _previousHitCount; ++i)
        {
            var index = (_previousHitCount + i) % HitCapacity;
            var hitDamage = _hitDamage.Get(index);
            OnDamaged?.Invoke(hitDamage);
        }
        _previousHitCount = HitCount;
    }
    
    public override void FixedUpdateNetwork()
    {
        _prevVelocity = _rigidbody.Rigidbody.linearVelocity;
    }

    public void OnCollisionEnter(Collision other)
    {
        if (false == Object.HasStateAuthority)
            return;

        var impact = Math.Max(
            _prevVelocity.sqrMagnitude,
            other.impulse.sqrMagnitude
        );

        if (impact < _damageLimit)
            return;

        var resistance = _weight * 0.3f + _hardness * 0.7f;
        var damage = impact / (1f + resistance);

        _condition = Mathf.Max(0f, _condition - damage);
        _hitDamage.Set(HitCount % HitCapacity, Mathf.RoundToInt(damage));
        _hitDamage.Set(HitCount % HitCapacity, Mathf.RoundToInt(damage));
        ++HitCount;
    }


}
