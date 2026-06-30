using TMPro;
using UnityEngine;

public class MoneyDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private string prefix = "Money: ";

    private int lastDisplayedMoney = int.MinValue;

    private void Update()
    {
        if (moneyText == null) return;

        NetworkGameManager manager = NetworkGameManager.Instance;
        if (manager == null)
        {
            SetText($"{prefix}---");
            return;
        }

        int current = manager.CurrentMoney;
        if (current == lastDisplayedMoney) return;

        lastDisplayedMoney = current;
        SetText($"{prefix}{current:N0}");
    }

    private void SetText(string value)
    {
        moneyText.text = value;
    }
}
