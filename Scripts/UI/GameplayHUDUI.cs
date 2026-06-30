using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayHUDUI : MonoBehaviour
{
    public enum ArmSlotSide
    {
        Left,
        Right
    }

    public enum BatteryFillMode
    {
        ImageFillAmount,
        RectHeight
    }

    [Header("Money")]
    [SerializeField] private TextMeshProUGUI currentMoneyText;
    [SerializeField] private TextMeshProUGUI targetMoneyText;

    [Header("Time")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Battery Health")]
    [SerializeField] private TextMeshProUGUI healthValueText;
    [SerializeField] private Image batteryCurrentFillImage;
    [SerializeField] private Image batteryMaxFillImage;
    [SerializeField] private BatteryFillMode batteryFillMode = BatteryFillMode.RectHeight;
    [SerializeField] private float minMaxBatteryRatio = 0.5f;

    [Header("Legacy Segment Display")]
    [SerializeField] private Image[] healthSegmentImages;
    [SerializeField] private Color healthActiveColor = Color.white;
    [SerializeField] private Color healthInactiveColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("Item Slots")]
    [SerializeField] private Image[] itemSlotIconImages;

    [Header("Special Arm Slot")]
    [SerializeField] private Image specialArmDefaultImage;
    [SerializeField] private Image specialArmIconImage;

    [Header("Test Preview")]
    [SerializeField] private bool useTestPreviewOnStart = true;
    [SerializeField] private int testCurrentBattery = 100;
    [SerializeField] private int testMaxBattery = 100;
    [SerializeField] private int testAbsoluteMaxBattery = 100;

    private RectTransform currentBatteryRect;
    private RectTransform maxBatteryRect;
    private float currentBatteryFullHeight;
    private float maxBatteryFullHeight;
    private float currentBatteryBottomY;
    private float maxBatteryBottomY;

    private void Awake()
    {
        CacheBatteryRects();
    }

    private void Start()
    {
        // Gameplay HUD는 GMP/NET/Fusion 또는 게임 시스템에서 확정된 값을 받아 표시만 한다.
        // 배터리 감소, 최대 충전량 감소, 사망 판정, 팔 장착 결과는 UI에서 직접 결정하지 않는다.
        ClearItemSlots();
        ClearSpecialArm();

        if (useTestPreviewOnStart)
        {
            SetBattery(testCurrentBattery, testMaxBattery, testAbsoluteMaxBattery);
        }
    }

    public void SetMoney(int currentMoney, int targetMoney)
    {
        SetText(currentMoneyText, FormatMoney(currentMoney));
        SetText(targetMoneyText, FormatMoney(targetMoney));
    }

    public void SetTime(string displayTime)
    {
        SetText(timeText, displayTime);
    }

    public void SetHealth(int currentHealth, int maxHealth)
    {
        SetBattery(currentHealth, maxHealth, maxHealth);
    }

    public void SetBattery(int currentBattery, int maxBattery, int absoluteMaxBattery)
    {
        // currentBattery: 현재 남은 배터리량이다.
        // maxBattery: 현재 충전 가능한 최대 배터리량이다. 몬스터 피해 등으로 줄어든 값을 받는다.
        // absoluteMaxBattery: 원래 기준 최대 배터리량이다. 100, 500 등 기획 변경에 맞춰 외부에서 넘겨주면 된다.
        int safeAbsoluteMaxBattery = Mathf.Max(1, absoluteMaxBattery);
        int minimumMaxBattery = Mathf.RoundToInt(safeAbsoluteMaxBattery * Mathf.Clamp01(minMaxBatteryRatio));
        int safeMaxBattery = Mathf.Clamp(maxBattery, minimumMaxBattery, safeAbsoluteMaxBattery);
        int safeCurrentBattery = Mathf.Clamp(currentBattery, 0, safeMaxBattery);

        float maxRatio = safeMaxBattery / (float)safeAbsoluteMaxBattery;
        float currentRatio = safeCurrentBattery / (float)safeAbsoluteMaxBattery;

        SetText(healthValueText, $"{safeCurrentBattery} / {safeMaxBattery}");
        ApplyBatteryImage(batteryMaxFillImage, maxBatteryRect, maxBatteryFullHeight, maxRatio);
        ApplyBatteryImage(batteryCurrentFillImage, currentBatteryRect, currentBatteryFullHeight, currentRatio);
        SetHealthSegments(currentRatio);
    }

    public void SetItemSlot(int slotIndex, Sprite icon)
    {
        Image slotImage = GetImageAt(itemSlotIconImages, slotIndex);
        if (slotImage == null)
        {
            return;
        }

        slotImage.sprite = icon;
        slotImage.enabled = icon != null;
    }

    public void ClearItemSlot(int slotIndex)
    {
        SetItemSlot(slotIndex, null);
    }

    public void ClearItemSlots()
    {
        if (itemSlotIconImages == null)
        {
            return;
        }

        for (int i = 0; i < itemSlotIconImages.Length; i++)
        {
            ClearItemSlot(i);
        }
    }

    public void SetArmSlot(ArmSlotSide side, Sprite icon)
    {
        SetSpecialArm(icon);
    }

    public void ClearArmSlot(ArmSlotSide side)
    {
        ClearSpecialArm();
    }

    public void SetSpecialArm(Sprite icon)
    {
        bool hasIcon = icon != null;

        if (specialArmIconImage != null)
        {
            specialArmIconImage.sprite = icon;
            specialArmIconImage.enabled = hasIcon;
        }

        if (specialArmDefaultImage != null)
        {
            specialArmDefaultImage.enabled = !hasIcon;
        }
    }

    public void ClearSpecialArm()
    {
        SetSpecialArm(null);
    }

    private void CacheBatteryRects()
    {
        currentBatteryRect = batteryCurrentFillImage != null ? batteryCurrentFillImage.rectTransform : null;
        maxBatteryRect = batteryMaxFillImage != null ? batteryMaxFillImage.rectTransform : null;
        currentBatteryFullHeight = currentBatteryRect != null ? currentBatteryRect.sizeDelta.y : 0f;
        maxBatteryFullHeight = maxBatteryRect != null ? maxBatteryRect.sizeDelta.y : 0f;
        currentBatteryBottomY = GetBottomEdge(currentBatteryRect);
        maxBatteryBottomY = GetBottomEdge(maxBatteryRect);
    }

    private void ApplyBatteryImage(Image image, RectTransform rect, float fullHeight, float ratio)
    {
        if (image == null)
        {
            return;
        }

        float safeRatio = Mathf.Clamp01(ratio);

        if (batteryFillMode == BatteryFillMode.ImageFillAmount)
        {
            image.fillAmount = safeRatio;
            return;
        }

        if (rect == null || fullHeight <= 0f)
        {
            return;
        }

        Vector2 size = rect.sizeDelta;
        size.y = fullHeight * safeRatio;
        rect.sizeDelta = size;

        Vector2 anchoredPosition = rect.anchoredPosition;
        anchoredPosition.y = GetAnchoredPositionForBottom(rect, size.y, rect == currentBatteryRect ? currentBatteryBottomY : maxBatteryBottomY);
        rect.anchoredPosition = anchoredPosition;
    }

    private static float GetBottomEdge(RectTransform rect)
    {
        if (rect == null)
        {
            return 0f;
        }

        return rect.anchoredPosition.y - (rect.sizeDelta.y * rect.pivot.y);
    }

    private static float GetAnchoredPositionForBottom(RectTransform rect, float height, float bottomEdge)
    {
        if (rect == null)
        {
            return 0f;
        }

        return bottomEdge + (height * rect.pivot.y);
    }

    private void SetHealthSegments(float batteryRatio)
    {
        if (healthSegmentImages == null || healthSegmentImages.Length == 0)
        {
            return;
        }

        float safeRatio = Mathf.Clamp01(batteryRatio);
        int activeCount = Mathf.CeilToInt(safeRatio * healthSegmentImages.Length);

        for (int i = 0; i < healthSegmentImages.Length; i++)
        {
            Image segmentImage = healthSegmentImages[i];
            if (segmentImage == null)
            {
                continue;
            }

            segmentImage.color = i < activeCount ? healthActiveColor : healthInactiveColor;
        }
    }

    private static Image GetImageAt(Image[] images, int index)
    {
        if (images == null || index < 0 || index >= images.Length)
        {
            return null;
        }

        return images[index];
    }

    private static void SetText(TextMeshProUGUI targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }

    private static string FormatMoney(int value)
    {
        return Mathf.Max(0, value).ToString("N0");
    }
}
