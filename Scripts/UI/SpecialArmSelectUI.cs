using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialArmSelectUI : MonoBehaviour
{
    [Serializable]
    public class SpecialArmSlot
    {
        [SerializeField] private Button slotButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image equippedImage;
        [SerializeField] private Sprite previewSprite;
        [TextArea]
        [SerializeField] private string description;

        public Button SlotButton => slotButton;
        public Image IconImage => iconImage;
        public Image EquippedImage => equippedImage;
        public Sprite PreviewSprite => previewSprite;
        public string Description => description;
    }

    [Header("Root")]
    [SerializeField] private GameObject rootObject;
    [SerializeField] private Button closeButton;

    [Header("Slots")]
    [SerializeField] private SpecialArmSlot[] armSlots;
    [SerializeField] private float normalAlpha = 1f;
    [SerializeField] private float selectedAlpha = 0.45f;
    [SerializeField] private bool hideButtonBackgroundImage = true;

    [Header("Preview")]
    [SerializeField] private Image previewImage;
    [SerializeField] private TextMeshProUGUI selectedArmNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Actions")]
    [SerializeField] private Button equipButton;
    [SerializeField] private bool applySelectionImmediatelyForMock = true;
    [SerializeField] private bool closeWithEscape = true;

    public event Action<int> EquipRequested;

    private int selectedIndex = -1;
    private int equippedIndex = -1;
    private bool isBound;

    private void Awake()
    {
        // 팔 선택창은 UI 입력과 표시만 담당한다.
        // 실제 팔 장착 가능 여부, 능력 적용, 네트워크 동기화는 GMP/NET/Fusion에서 확정한다.
        BindButtons();
        RefreshAllSlots();
        RefreshPreview();
    }

    private void OnEnable()
    {
        RefreshAllSlots();
        RefreshPreview();
    }

    private void Update()
    {
        if (closeWithEscape && IsOpen() && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    public void Open()
    {
        SetRootActive(true);
        selectedIndex = equippedIndex;
        RefreshAllSlots();
        RefreshPreview();
    }

    public void Close()
    {
        selectedIndex = equippedIndex;
        RefreshAllSlots();
        RefreshPreview();
        SetRootActive(false);
    }

    public void SelectSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
        {
            selectedIndex = -1;
            RefreshAllSlots();
            RefreshPreview();
            return;
        }

        if (slotIndex == equippedIndex)
        {
            return;
        }

        selectedIndex = slotIndex;
        RefreshAllSlots();
        RefreshPreview();
    }

    public void ApplyEquippedArm(int slotIndex)
    {
        // 장착 확정 결과를 받은 뒤 호출한다.
        // UI가 직접 확정하지 않고, 외부 시스템에서 결정한 인덱스를 표시한다.
        equippedIndex = IsValidSlot(slotIndex) ? slotIndex : -1;
        selectedIndex = equippedIndex;
        RefreshAllSlots();
        RefreshPreview();
    }

    public void ClearEquippedArm()
    {
        ApplyEquippedArm(-1);
    }

    public void OnClickEquipButton()
    {
        if (!IsValidSlot(selectedIndex) || selectedIndex == equippedIndex)
        {
            return;
        }

        EquipRequested?.Invoke(selectedIndex);

        // Test_UIFlow에서 단독 확인할 수 있도록 켜두는 Mock 표시 옵션이다.
        // 실제 연동 시에는 끄고, GMP/NET/Fusion 확정 후 ApplyEquippedArm을 호출한다.
        if (applySelectionImmediatelyForMock)
        {
            ApplyEquippedArm(selectedIndex);
        }
    }

    private void BindButtons()
    {
        if (isBound)
        {
            return;
        }

        isBound = true;

        if (armSlots != null)
        {
            for (int i = 0; i < armSlots.Length; i++)
            {
                int slotIndex = i;
                Button slotButton = armSlots[i]?.SlotButton;
                if (slotButton != null)
                {
                    slotButton.onClick.AddListener(() => SelectSlot(slotIndex));
                }
            }
        }

        if (equipButton != null)
        {
            equipButton.onClick.AddListener(OnClickEquipButton);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    private void RefreshAllSlots()
    {
        if (armSlots == null)
        {
            return;
        }

        for (int i = 0; i < armSlots.Length; i++)
        {
            RefreshSlot(i);
        }
    }

    private void RefreshSlot(int slotIndex)
    {
        SpecialArmSlot slot = armSlots[slotIndex];
        if (slot == null)
        {
            return;
        }

        if (hideButtonBackgroundImage)
        {
            SetButtonBackgroundAlpha(slot.SlotButton, 0f);
        }

        bool isSelected = slotIndex == selectedIndex;
        bool isEquipped = slotIndex == equippedIndex;

        SetImageAlpha(slot.IconImage, isSelected || isEquipped ? selectedAlpha : normalAlpha);
        SetEquippedImageActive(slot.EquippedImage, isEquipped);

        if (slot.SlotButton != null)
        {
            slot.SlotButton.interactable = !isEquipped;
        }
    }

    private void RefreshPreview()
    {
        if (previewImage == null)
        {
            return;
        }

        SpecialArmSlot selectedSlot = IsValidSlot(selectedIndex) ? armSlots[selectedIndex] : null;
        Sprite previewSprite = selectedSlot?.PreviewSprite != null ? selectedSlot.PreviewSprite : selectedSlot?.IconImage?.sprite;

        previewImage.sprite = previewSprite;
        previewImage.enabled = true;

        if (selectedArmNameText != null)
        {
            selectedArmNameText.text = IsValidSlot(selectedIndex) ? $"ARM {selectedIndex + 1}" : string.Empty;
        }

        if (descriptionText != null)
        {
            // 설명도 클릭한 팔 기준으로 표시한다. 실제 팔 성능 확정은 UI가 하지 않는다.
            descriptionText.text = selectedSlot != null ? selectedSlot.Description : string.Empty;
        }
    }

    private bool IsOpen()
    {
        GameObject targetRoot = rootObject != null ? rootObject : gameObject;
        return targetRoot.activeSelf;
    }

    private void SetRootActive(bool isActive)
    {
        GameObject targetRoot = rootObject != null ? rootObject : gameObject;
        targetRoot.SetActive(isActive);
    }

    private bool IsValidSlot(int slotIndex)
    {
        return armSlots != null && slotIndex >= 0 && slotIndex < armSlots.Length && armSlots[slotIndex] != null;
    }

    private void SetEquippedImageActive(Image targetImage, bool isActive)
    {
        if (targetImage == null)
        {
            return;
        }

        if (equipButton != null && targetImage.gameObject == equipButton.gameObject)
        {
            // Inspector에 장착 버튼이 실수로 Equipped Image에 연결되어도 버튼이 꺼지지 않도록 막는다.
            return;
        }

        if (closeButton != null && targetImage.gameObject == closeButton.gameObject)
        {
            // 닫기 버튼이 잘못 연결된 경우도 창을 닫을 수 있도록 버튼 비활성화를 막는다.
            return;
        }

        targetImage.gameObject.SetActive(isActive);
    }

    private static void SetImageAlpha(Image targetImage, float alpha)
    {
        if (targetImage == null)
        {
            return;
        }

        Color color = targetImage.color;
        color.a = Mathf.Clamp01(alpha);
        targetImage.color = color;
    }

    private static void SetButtonBackgroundAlpha(Button targetButton, float alpha)
    {
        if (targetButton == null || targetButton.image == null)
        {
            return;
        }

        Color color = targetButton.image.color;
        color.a = Mathf.Clamp01(alpha);
        targetButton.image.color = color;
    }
}
