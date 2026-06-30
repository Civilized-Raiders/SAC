using System;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using Random = UnityEngine.Random;

public class SalvageDebug : NetworkBehaviour
{
#if UNITY_EDITOR
    private NetworkRigidbody3D _rigidbody;
    private SalvageDamageHandler _salvageDamageHandler;

    [Header("Debug")]
    [SerializeField] private bool _showLog;
    [SerializeField] private bool _randomJump;
    private float _time;

    public void Awake()
    {
        _rigidbody = GetComponent<NetworkRigidbody3D>();
        _salvageDamageHandler = GetComponent<SalvageDamageHandler>();
    }

    public override void Spawned()
    {
        _salvageDamageHandler.OnDamaged += ShowLog;
    }
    public override void FixedUpdateNetwork()
    {
        RandomJump();
    }

    private void ShowLog(int damage)
    {
        if (false == _showLog)
            return;
        Debug.Log(damage);
    }
    private void RandomJump()
    {
        if (false == _randomJump)
            return;
        _time += Runner.DeltaTime;
        if (5 <= _time)
        {
            var randomDirection = new Vector3(
                Random.Range(-1f, 1f),
                1f, 
                Random.Range(-1f, 1f)
            ).normalized;
            _rigidbody.Rigidbody.AddForce(randomDirection * 10, ForceMode.Impulse);
            
            var randomTorque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized;
            _rigidbody.Rigidbody.AddTorque(randomTorque * 10, ForceMode.Impulse);
            _time = 0f;
        }
    }
#endif
}
