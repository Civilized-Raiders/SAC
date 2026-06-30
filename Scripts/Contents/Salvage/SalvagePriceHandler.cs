using System;
using Fusion;
using UnityEngine;
using UnityEngine.Pool;

public class SalvagePriceHandler : NetworkBehaviour, ISalvageSetData
{
    [SerializeField] private SalvagePriceUI _salvagePriceUIPrefab;
    private SalvagePriceUI _salvagePriceUI;
    
    [SerializeField] private SalvageDamageUI _salvageDamageUIPrefab;
    private SalvageDamageHandler _damageHandler;
    private int _salvagePrice = 500;
    public int CurrentPrice { get; private set; }
    public event Action OnAllDecreased;
    public override void Spawned()
    {
        _damageHandler = GetComponent<SalvageDamageHandler>();
        CurrentPrice = _salvagePrice;

        _salvagePriceUI = Instantiate(_salvagePriceUIPrefab, transform.position, Quaternion.identity);
        _salvagePriceUI.SetTarget(transform);
        _salvagePriceUI.SetPrice(CurrentPrice);
        _salvagePriceUI.gameObject.SetActive(false);
        _damageHandler.OnDamaged += OnDamage;
    }

    private void OnDamage(int damage)
    {
        var damageUI = Instantiate(_salvageDamageUIPrefab, transform.position, Quaternion.identity);
        var damagePrice = Mathf.RoundToInt(_salvagePrice * (damage / 100f));
        damageUI.ShowDamageUI(damagePrice);
        CurrentPrice -= damagePrice;
        
        _salvagePriceUI.SetPrice(CurrentPrice);
        if (CurrentPrice < 0)
            AllDecreased();
    }

    private void AllDecreased()
    {
        OnAllDecreased?.Invoke();
        if (Object.HasStateAuthority)
            Runner.Despawn(Object);
    }
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_damageHandler != null)
            _damageHandler.OnDamaged -= OnDamage;
        Destroy(_salvagePriceUI.gameObject);
    }

    public void Set(SalvageData data)
    {
        _salvagePrice = data.salvagePrice;
    }
    public void SetPriceVisible(bool visible)
    {
        if(_salvagePriceUI == null)
        return;

        if (visible)
            _salvagePriceUI.SnapToTarget();

        _salvagePriceUI.gameObject.SetActive(visible);
    }
}
