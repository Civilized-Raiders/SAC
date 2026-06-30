using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomColorSelectUI : MonoBehaviour
{
    [Serializable]
    public class ColorButtonEntry
    {
        public string colorId;
        public Button button;
        public Image selectedImage;
        public Image lockedImage;
    }

    [Header("Title")]
    [SerializeField] private TextMeshProUGUI colorSelectTitleText;

    [Header("Color Buttons")]
    [SerializeField] private ColorButtonEntry[] colorButtons;

    public event Action<string> ColorSelectRequested;

    private string selectedColorId = string.Empty;
    private string[] lockedColorIds = Array.Empty<string>();
    private bool listenersAdded;

    private void Awake()
    {
        AutoBind();
        PrepareRaycastTargets();
    }

    private void Start()
    {
        if (colorSelectTitleText != null)
        {
            colorSelectTitleText.text = "색상 선택";
        }

        RefreshButtons();
    }

    private void OnEnable()
    {
        AutoBind();
        PrepareRaycastTargets();
        AddButtonListeners();
    }

    // 색상 버튼은 내 플레이어 색상 변경 요청만 보낸다.
    // 이미 다른 플레이어가 선점한 색상인지 최종 판단하는 것은 Fusion 쪽 책임이다.
    public void SetSelectedColor(string colorId)
    {
        selectedColorId = colorId;
        RefreshButtons();
    }

    // 다른 플레이어가 이미 사용 중인 색상은 선택할 수 없게 표시한다.
    // 동시 선택 문제가 생길 수 있으므로, 이 값도 나중에는 Fusion에서 확정된 상태를 받아야 한다.
    public void SetLockedColors(string[] colorIds)
    {
        lockedColorIds = colorIds ?? Array.Empty<string>();
        RefreshButtons();
    }

    public void OnClickColorButton(string colorId)
    {
        if (IsLocked(colorId))
        {
            return;
        }

        // Fusion sync before the visual can change in some cases,
        // so reflect the local choice immediately and let synced data overwrite if needed.
        SetSelectedColor(colorId);
        ColorSelectRequested?.Invoke(colorId);
    }

    private void RefreshButtons()
    {
        if (colorButtons == null)
        {
            return;
        }

        foreach (ColorButtonEntry entry in colorButtons)
        {
            if (entry == null)
            {
                continue;
            }

            bool isLocked = IsLocked(entry.colorId);
            bool isSelected = entry.colorId == selectedColorId;

            if (entry.button != null)
            {
                entry.button.interactable = !isLocked;
                ApplyFallbackButtonVisual(entry.button, isSelected, isLocked);
            }

            SetObjectActive(entry.selectedImage, isSelected);
            SetObjectActive(entry.lockedImage, isLocked);
        }
    }

    private void PrepareRaycastTargets()
    {
        if (colorSelectTitleText != null)
        {
            colorSelectTitleText.raycastTarget = false;
        }

        if (colorButtons == null)
        {
            return;
        }

        foreach (ColorButtonEntry entry in colorButtons)
        {
            if (entry == null)
            {
                continue;
            }

            // 버튼의 실제 클릭 판정은 Button 쪽 Graphic만 담당한다.
            // 선택/잠금 표시는 장식 이미지이므로 Raycast를 꺼서 클릭을 가로막지 않게 한다.
            PrepareButtonHierarchy(entry.button);

            if (entry.button != null && entry.button.targetGraphic != null)
            {
                entry.button.targetGraphic.raycastTarget = true;
            }

            SetRaycastTarget(entry.selectedImage, false);
            SetRaycastTarget(entry.lockedImage, false);
        }
    }

    private bool IsLocked(string colorId)
    {
        if (lockedColorIds == null)
        {
            return false;
        }

        for (int i = 0; i < lockedColorIds.Length; i++)
        {
            if (lockedColorIds[i] == colorId)
            {
                return true;
            }
        }

        return false;
    }

    private void AddButtonListeners()
    {
        if (listenersAdded)
        {
            return;
        }

        if (colorButtons == null)
        {
            return;
        }

        foreach (ColorButtonEntry entry in colorButtons)
        {
            if (entry == null || entry.button == null)
            {
                continue;
            }

            string colorId = entry.colorId;
            entry.button.onClick.AddListener(() => OnClickColorButton(colorId));
        }

        listenersAdded = true;
    }

    private static void SetObjectActive(Component target, bool isActive)
    {
        if (target != null)
        {
            target.gameObject.SetActive(isActive);
        }
    }

    private static void SetRaycastTarget(Graphic graphic, bool isRaycastTarget)
    {
        if (graphic != null)
        {
            graphic.raycastTarget = isRaycastTarget;
        }
    }

    private void AutoBind()
    {
        colorSelectTitleText = colorSelectTitleText != null ? colorSelectTitleText : FindText("ColorSelectTitleText");

        if (colorButtons == null || colorButtons.Length == 0)
        {
            colorButtons = BuildColorButtonEntries();
            return;
        }

        for (int i = 0; i < colorButtons.Length; i++)
        {
            ColorButtonEntry entry = colorButtons[i];
            if (entry == null)
            {
                continue;
            }

            if (entry.button == null && !string.IsNullOrEmpty(entry.colorId))
            {
                entry.button = FindButton($"ColorButton_{entry.colorId}");
            }

            if (entry.selectedImage == null && entry.button != null)
            {
                entry.selectedImage = FindOverlayImage(entry.button.transform, "Check");
            }

            if (entry.lockedImage == null && entry.button != null)
            {
                entry.lockedImage = FindOverlayImage(entry.button.transform, "Locked");
            }
        }
    }

    private TextMeshProUGUI FindText(string childName)
    {
        Transform child = FindChild(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private Button FindButton(string childName)
    {
        Transform child = FindChild(childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private Transform FindChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private ColorButtonEntry[] BuildColorButtonEntries()
    {
        string[] knownColorIds = { "Yellow", "Red", "Blue", "Green", "Black", "Purple", "White" };
        System.Collections.Generic.List<ColorButtonEntry> entries = new System.Collections.Generic.List<ColorButtonEntry>();

        for (int i = 0; i < knownColorIds.Length; i++)
        {
            Button button = FindButton($"ColorButton_{knownColorIds[i]}");
            if (button == null)
            {
                continue;
            }

            entries.Add(new ColorButtonEntry
            {
                colorId = knownColorIds[i],
                button = button,
                selectedImage = FindOverlayImage(button.transform, "Check"),
                lockedImage = FindOverlayImage(button.transform, "Locked"),
            });
        }

        return entries.ToArray();
    }

    private Image FindOverlayImage(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private static void ApplyFallbackButtonVisual(Button button, bool isSelected, bool isLocked)
    {
        Graphic targetGraphic = button != null ? button.targetGraphic : null;
        if (targetGraphic == null)
        {
            return;
        }

        Color color = Color.white;
        if (isLocked)
        {
            color.a = 0.35f;
        }
        else if (!isSelected)
        {
            color.a = 0.72f;
        }

        targetGraphic.color = color;
        button.transform.localScale = isSelected ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
    }

    private static void PrepareButtonHierarchy(Button button)
    {
        if (button == null)
        {
            return;
        }

        Graphic targetGraphic = button.targetGraphic;
        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            graphic.raycastTarget = graphic == targetGraphic;
        }
    }
}
