using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(
    typeof(SalvageDamageHandler), 
    typeof(SalvagePriceHandler),
    typeof(SalvageDebug))]
public class SalvageEntity : MonoBehaviour
{
    [Header("ScriptableObject")]
    [SerializeField] private SalvageData data;
    
    [Header("Handler")]
    [SerializeField] private SalvagePriceHandler _salvagePriceHandler;
    [SerializeField] private SalvageDamageHandler _salvageDamageHandler;
    private void Awake()
    {
        _salvagePriceHandler = GetComponent<SalvagePriceHandler>();
        _salvageDamageHandler = GetComponent<SalvageDamageHandler>();
        var components = GetComponents<ISalvageSetData>();
        foreach (var salvageSetData in components)
            salvageSetData.Set(data);
    }

    public int CurrentPrice() => _salvagePriceHandler.CurrentPrice;
}
