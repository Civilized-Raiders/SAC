using System;
using UnityEngine;
using UnityEngine.UI;

public class RoomMapSelectUI : MonoBehaviour
{
    [Serializable]
    public class MapSlot
    {
        [Tooltip("동기화나 저장에서 사용할 맵 식별자입니다.")]
        public string mapId;

        [Tooltip("실제 이동 대상 씬 이름입니다. UI는 이 값을 확정하지 않고 외부 시스템에 전달만 합니다.")]
        public string sceneName;

        [Tooltip("맵 이미지 안에 이름까지 포함해 넣는 구조입니다.")]
        public Sprite previewSprite;

        [Tooltip("아직 제작되지 않은 맵이면 false로 두어 시작 조건에서 제외합니다.")]
        public bool isAvailable;
    }

    [Header("UI References")]
    [SerializeField] private Image mapPreviewImage;
    [SerializeField] private Image mapLockedImage;
    [SerializeField] private Button mapPrevButton;
    [SerializeField] private Button mapNextButton;

    [Header("Map Slots")]
    [SerializeField] private MapSlot[] mapSlots = new MapSlot[3];
    [SerializeField] private int selectedIndex;

    private bool canControl;

    public event Action<int> MapSelectionChangeRequested;

    public int SelectedIndex => selectedIndex;
    public bool HasPlayableSelectedMap => GetSelectedMap() != null && GetSelectedMap().isAvailable;
    public string SelectedSceneName => GetSelectedMap()?.sceneName;
    public string SelectedMapId => GetSelectedMap()?.mapId;

    private void Awake()
    {
        AutoBind();
        PrepareRaycastTargets();
        ClampSelectedIndex();
        Refresh();
    }

    private void OnEnable()
    {
        AutoBind();
        PrepareRaycastTargets();
        AddButtonListeners();
        Refresh();
    }

    private void OnDisable()
    {
        RemoveButtonListeners();
    }

    public void SetHostControl(bool isHost)
    {
        canControl = isHost;
        RefreshInteractable();
    }

    public void ApplySyncedMapIndex(int index)
    {
        selectedIndex = index;
        ClampSelectedIndex();
        Refresh();
    }

    public void ApplySyncedMapId(string mapId)
    {
        int index = GetMapIndexById(mapId);
        if (index < 0)
        {
            return;
        }

        ApplySyncedMapIndex(index);
    }

    public int GetMapIndexById(string mapId)
    {
        if (string.IsNullOrEmpty(mapId) || mapSlots == null)
        {
            return -1;
        }

        for (int i = 0; i < mapSlots.Length; i++)
        {
            MapSlot slot = mapSlots[i];
            if (slot != null && slot.mapId == mapId)
            {
                return i;
            }
        }

        return -1;
    }

    public MapSlot GetSelectedMap()
    {
        if (mapSlots == null || mapSlots.Length == 0)
        {
            return null;
        }

        if (selectedIndex < 0 || selectedIndex >= mapSlots.Length)
        {
            return null;
        }

        return mapSlots[selectedIndex];
    }

    private void OnClickPrevButton()
    {
        if (!canControl || mapSlots == null || mapSlots.Length == 0)
        {
            return;
        }

        selectedIndex--;
        if (selectedIndex < 0)
        {
            selectedIndex = mapSlots.Length - 1;
        }

        // UI는 선택 요청만 발생시킨다. 실제 확정과 동기화는 NET/Fusion 담당 시스템에서 처리한다.
        MapSelectionChangeRequested?.Invoke(selectedIndex);
        Refresh();
    }

    private void OnClickNextButton()
    {
        if (!canControl || mapSlots == null || mapSlots.Length == 0)
        {
            return;
        }

        selectedIndex++;
        if (selectedIndex >= mapSlots.Length)
        {
            selectedIndex = 0;
        }

        // 방장 입력만 받아 선택 요청을 보내고, 다른 플레이어는 동기화된 결과를 표시만 한다.
        MapSelectionChangeRequested?.Invoke(selectedIndex);
        Refresh();
    }

    private void Refresh()
    {
        ClampSelectedIndex();

        MapSlot slot = GetSelectedMap();
        if (mapPreviewImage != null)
        {
            mapPreviewImage.sprite = slot != null ? slot.previewSprite : null;
            mapPreviewImage.enabled = slot != null && slot.previewSprite != null;
        }

        if (mapLockedImage != null)
        {
            mapLockedImage.gameObject.SetActive(slot == null || !slot.isAvailable);
        }

        RefreshInteractable();
    }

    private void RefreshInteractable()
    {
        bool canMove = canControl && mapSlots != null && mapSlots.Length > 1;

        if (mapPrevButton != null)
        {
            mapPrevButton.interactable = canMove;
        }

        if (mapNextButton != null)
        {
            mapNextButton.interactable = canMove;
        }
    }

    private void ClampSelectedIndex()
    {
        if (mapSlots == null || mapSlots.Length == 0)
        {
            selectedIndex = 0;
            return;
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, mapSlots.Length - 1);
    }

    private void PrepareRaycastTargets()
    {
        // 맵 미리보기/잠금 이미지는 표시용이다.
        // 좌우 버튼 클릭을 막지 않도록 Raycast를 꺼두고, 실제 입력은 Button만 받게 한다.
        SetRaycastTarget(mapPreviewImage, false);
        SetRaycastTarget(mapLockedImage, false);

        PrepareButtonHierarchy(mapPrevButton);
        PrepareButtonHierarchy(mapNextButton);
    }

    private void AddButtonListeners()
    {
        if (mapPrevButton != null)
        {
            mapPrevButton.onClick.RemoveListener(OnClickPrevButton);
            mapPrevButton.onClick.AddListener(OnClickPrevButton);
        }

        if (mapNextButton != null)
        {
            mapNextButton.onClick.RemoveListener(OnClickNextButton);
            mapNextButton.onClick.AddListener(OnClickNextButton);
        }
    }

    private void RemoveButtonListeners()
    {
        if (mapPrevButton != null)
        {
            mapPrevButton.onClick.RemoveListener(OnClickPrevButton);
        }

        if (mapNextButton != null)
        {
            mapNextButton.onClick.RemoveListener(OnClickNextButton);
        }
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

    private static void SetRaycastTarget(Graphic graphic, bool isRaycastTarget)
    {
        if (graphic != null)
        {
            graphic.raycastTarget = isRaycastTarget;
        }
    }

    private void AutoBind()
    {
        mapPreviewImage = mapPreviewImage != null ? mapPreviewImage : FindImage("MapPreviewPanel");
        mapLockedImage = mapLockedImage != null ? mapLockedImage : FindImage("MapLockedImage");
        mapPrevButton = mapPrevButton != null ? mapPrevButton : FindButton("MapPrevButton");
        mapNextButton = mapNextButton != null ? mapNextButton : FindButton("MapNextButton");
    }

    private Image FindImage(string childName)
    {
        Transform child = FindChild(childName);
        return child != null ? child.GetComponent<Image>() : null;
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
}
