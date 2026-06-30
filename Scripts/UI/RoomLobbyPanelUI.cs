using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomLobbyPanelUI : MonoBehaviour
{
    [Header("Top Area")]
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI publicStateText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Button lobbySettingButton;

    [SerializeField] private UIAudioVolumeController audioVolumeController; // 사운드 관련 추가

    [Header("Panels")]
    [SerializeField] private LobbySettingPanelUI lobbySettingPanel;
    [SerializeField] private GameObject lobbySettingPanelRoot;

    [Header("Temporary Display Data")]
    [SerializeField] private bool useTemporaryRoomInfoOnStart = true;
    [SerializeField] private string[] temporaryRoomNames =
    {
        "고철 수거대",
        "폐허 탐사대",
        "야간 회수조",
        "정비 구역",
        "회수 작전실"
    };

    private void Awake()
    {
        AutoBind();
        PrepareRaycastTargets();
    }

    private void Start()
    {
        AutoBind();

        if (useTemporaryRoomInfoOnStart)
        {
            ShowTemporaryRoomInfo();
        }

        if (lobbySettingPanel != null)
        {
            lobbySettingPanel.Hide();
        }
        else if (lobbySettingPanelRoot != null)
        {
            lobbySettingPanelRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        AutoBind();
        PrepareRaycastTargets();

        if (lobbySettingButton != null)
        {
            lobbySettingButton.onClick.RemoveListener(OnClickLobbySettingButton);
            lobbySettingButton.onClick.AddListener(OnClickLobbySettingButton);
        }
    }

    private void OnDisable()
    {
        if (lobbySettingButton != null)
        {
            lobbySettingButton.onClick.RemoveListener(OnClickLobbySettingButton);
        }
    }

    // TopArea에 표시되는 방 정보만 갱신한다.
    // 실제 방 이름, 공개 여부, 인원수는 나중에 Fusion에서 확정된 값을 받아서 넣는다.
    public void SetRoomInfo(string roomName, bool isPrivateRoom, int currentPlayerCount, int maxPlayerCount)
    {
        if (roomNameText != null)
        {
            roomNameText.text = roomName;
        }

        if (publicStateText != null)
        {
            publicStateText.text = isPrivateRoom ? "비밀방" : "공개방";
        }

        SetPlayerCount(currentPlayerCount, maxPlayerCount);
    }

    // 현재 인원수 표시는 UI 표시 전용이다.
    // 참가자 입장/퇴장 판정은 UI가 직접 하지 않고 Fusion 쪽 방 상태를 기준으로 한다.
    public void SetPlayerCount(int currentPlayerCount, int maxPlayerCount)
    {
        if (playerCountText == null)
        {
            return;
        }

        int safeMaxPlayerCount = Mathf.Max(1, maxPlayerCount);
        int safeCurrentPlayerCount = Mathf.Clamp(currentPlayerCount, 0, safeMaxPlayerCount);
        playerCountText.text = $"{safeCurrentPlayerCount}/{safeMaxPlayerCount}";
    }

    // 설정 버튼은 방 설정이 아니라 개인 사운드/효과음 설정 패널을 연다.
    // 이 설정은 다른 플레이어와 동기화하지 않는 로컬 UI 옵션으로 다룬다.
    public void OnClickLobbySettingButton()
    {
        PlayButtonClick(); // 사운드 관련 추가

        if (lobbySettingPanel != null)
        {
            if (lobbySettingPanel.gameObject.activeSelf)
            {
                lobbySettingPanel.Hide();
            }
            else
            {
                lobbySettingPanel.Show();
            }
            return;
        }

        if (lobbySettingPanelRoot != null)
        {
            lobbySettingPanelRoot.SetActive(!lobbySettingPanelRoot.activeSelf);
        }
    }

    // Fusion 연결 전 RoomLobby 화면 확인용 임시 데이터다.
    // 실제 멀티 연결 후에는 Host가 정한 방 이름과 방 상태를 SetRoomInfo로 전달받아 표시한다.
    private void ShowTemporaryRoomInfo()
    {
        string roomName = "ROOM NAME";
        if (temporaryRoomNames != null && temporaryRoomNames.Length > 0)
        {
            int index = Random.Range(0, temporaryRoomNames.Length);
            roomName = temporaryRoomNames[index];
        }

        SetRoomInfo(roomName, false, 1, 4);
    }

    private void AutoBind()
    {
        roomNameText = roomNameText != null ? roomNameText : FindText("RoomNameText");
        publicStateText = publicStateText != null ? publicStateText : FindText("PublicStateText");
        playerCountText = playerCountText != null ? playerCountText : FindText("PlayerCountText");
        lobbySettingButton = lobbySettingButton != null ? lobbySettingButton : FindButton("LobbySettingButton");
        
        audioVolumeController = audioVolumeController != null ? audioVolumeController : FindFirstObjectByType<UIAudioVolumeController>(); // 사운드 관련 추가
        lobbySettingPanel = lobbySettingPanel != null ? lobbySettingPanel : FindFirstObjectByType<LobbySettingPanelUI>(FindObjectsInactive.Include);


        bool needsSettingPanelRoot =
            lobbySettingPanelRoot == null ||
            lobbySettingPanelRoot == gameObject ||
            (lobbySettingButton != null && lobbySettingPanelRoot == lobbySettingButton.gameObject);

        if (needsSettingPanelRoot)
        {
            if (lobbySettingPanel != null)
            {
                lobbySettingPanelRoot = lobbySettingPanel.gameObject;
            }
            else
            {
                Transform panelTransform = FindChild("LobbySettingPanel");
                lobbySettingPanelRoot = panelTransform != null ? panelTransform.gameObject : null;
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

    private void PrepareRaycastTargets()
    {
        SetRaycastTarget(roomNameText, false);
        SetRaycastTarget(publicStateText, false);
        SetRaycastTarget(playerCountText, false);
        PrepareButtonHierarchy(lobbySettingButton);

        Transform topArea = FindChild("TopArea");
        if (topArea != null)
        {
            SetRaycastTarget(topArea.GetComponent<Graphic>(), false);
        }
    }

    private static void SetRaycastTarget(Graphic graphic, bool isRaycastTarget)
    {
        if (graphic != null)
        {
            graphic.raycastTarget = isRaycastTarget;
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

    // 사운드 관련 추가
    private void PlayButtonClick()
    {
        if (audioVolumeController != null)
        {
            audioVolumeController.PlayButtonClick();
        }
    }
}
