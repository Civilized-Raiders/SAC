using UnityEngine;

[CreateAssetMenu(fileName = "SalvageData", menuName = "Scriptable Objects/SalvageData")]
public class SalvageData : ScriptableObject
{
    public string salvageName;  
    public int salvagePrice = 500;
    [Tooltip("회수품의 무게. 높을수록 충돌 데미지를 적게 받음(0.3)")]
    public float weight = 10f;      // 회수품 무게
    
    [Tooltip("회수품의 경도. 높을수록 충돌 데미지를 적게 받음(0.7)")]
    public float hardness = 5f;     // 회수품의 방어력
    
    [Tooltip("충격 제한. 해당 값보다 낮은 데미지는 무시합")]
    public float damageLimit = 50;  // 해당 값보다 낮은 데미지는 무시
}
