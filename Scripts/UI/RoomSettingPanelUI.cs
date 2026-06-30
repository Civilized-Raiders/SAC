using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomSettingPanelUI : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private TextMeshProUGUI roomSettingTitleText;

    [Header("Max Player")]
    [SerializeField] private TextMeshProUGUI maxPlayerLabelText;
    [SerializeField] private Button maxPlayerPrevButton;
    [SerializeField] private TextMeshProUGUI maxPlayerValueText;
    [SerializeField] private Button maxPlayerNextButton;

    [Header("Private State")]
    [SerializeField] private TextMeshProUGUI privateStateLabelText;
    [SerializeField] private Button privateStateToggleButton;
    [SerializeField] private TextMeshProUGUI privateStateValueText;

    [Header("Room Code")]
    [SerializeField] private TextMeshProUGUI roomCodeLabelText;
    [SerializeField] private TextMeshProUGUI roomCodeValueText;
    [SerializeField] private Button changeRoomCodeButton;

    [Header("Change Room Code Input")]
    [SerializeField] private GameObject changeRoomCodeInputRoot;
    [SerializeField] private TMP_InputField changeRoomCodeInput;
    [SerializeField] private Button confirmRoomCodeButton;

    [Header("Rule")]
    [SerializeField] private int minPlayerCount = 1;
    [SerializeField] private int maxPlayerCountLimit = 4;

    public event Action<int> MaxPlayerChangeRequested;
    public event Action<bool> PrivateStateChangeRequested;
    public event Action ChangeRoomCodeRequested;

    private bool isHost;
    private bool isPrivateRoom;
    private int currentPlayerCount = 1;
    private int maxPlayerCount = 4;
    private string roomCode = string.Empty;

    private void Start()
    {
        SetStaticTexts();
        SetObjectActive(changeRoomCodeInputRoot, false);
        RefreshView();
    }

    private void OnEnable()
    {
        AddButtonListeners();
    }

    private void OnDisable()
    {
        RemoveButtonListeners();
    }

    // 방 설정 영역은 Host만 조작할 수 있다.
    // Guest도 변경된 결과는 볼 수 있지만, 직접 버튼을 눌러 방 정보를 바꾸지는 못한다.
    public void SetHostControl(bool canControl)
    {
        isHost = canControl;
        RefreshInteractable();
    }

    // 방 설정 정보는 UI가 직접 확정하지 않는다.
    // 나중에 Fusion에서 확정된 방 상태를 받아 이 함수로 표시한다.
    public void SetRoomSettingInfo(int currentPlayers, int maxPlayers, bool privateRoom, string code)
    {
        currentPlayerCount = Mathf.Max(0, currentPlayers);
        maxPlayerCount = Mathf.Clamp(maxPlayers, minPlayerCount, maxPlayerCountLimit);
        isPrivateRoom = privateRoom;
        roomCode = privateRoom ? code : string.Empty;
        RefreshView();
    }

    public void OnClickMaxPlayerPrevButton()
    {
        if (!isHost)
        {
            return;
        }

        int nextMaxPlayerCount = maxPlayerCount - 1;
        if (nextMaxPlayerCount < currentPlayerCount || nextMaxPlayerCount < minPlayerCount)
        {
            return;
        }

        MaxPlayerChangeRequested?.Invoke(nextMaxPlayerCount);
    }

    public void OnClickMaxPlayerNextButton()
    {
        if (!isHost)
        {
            return;
        }

        int nextMaxPlayerCount = maxPlayerCount + 1;
        if (nextMaxPlayerCount > maxPlayerCountLimit)
        {
            return;
        }

        MaxPlayerChangeRequested?.Invoke(nextMaxPlayerCount);
    }

    public void OnClickPrivateStateToggleButton()
    {
        if (!isHost)
        {
            return;
        }

        // 공개방에서 비밀방으로 바꿀 때는 나중에 코드 입력 UI를 열어야 한다.
        // 지금은 요청 이벤트만 보내고, 실제 코드 확정은 Fusion/방 설정 쪽에서 처리한다.
        PrivateStateChangeRequested?.Invoke(!isPrivateRoom);
    }

    public void OnClickChangeRoomCodeButton()
    {
        if (!isHost || !isPrivateRoom)
        {
            return;
        }

        // 비밀방 코드 변경은 별도 입력 팝업이 필요하다.
        // 지금은 입력 UI를 열고, Confirm 버튼에서 임시 표시 값을 갱신한다.
        SetObjectActive(changeRoomCodeInputRoot, true);
        if (changeRoomCodeInput != null)
        {
            changeRoomCodeInput.text = roomCode;
            changeRoomCodeInput.Select();
        }

        ChangeRoomCodeRequested?.Invoke();
    }

    public void OnClickConfirmRoomCodeButton()
    {
        if (!isHost || !isPrivateRoom || changeRoomCodeInput == null)
        {
            return;
        }

        string nextRoomCode = changeRoomCodeInput.text.Trim();
        if (string.IsNullOrEmpty(nextRoomCode))
        {
            return;
        }

        // 현재는 UI 확인용 임시 표시다.
        // 실제 방 코드 변경은 Fusion/서버에서 성공 확정된 뒤 SetRoomSettingInfo로 다시 표시해야 한다.
        roomCode = nextRoomCode;
        SetText(roomCodeValueText, roomCode);
        SetObjectActive(changeRoomCodeInputRoot, false);
    }

    private void SetStaticTexts()
    {
        SetText(roomSettingTitleText, "룸 설정");
        SetText(maxPlayerLabelText, "최대 인원");
        SetText(privateStateLabelText, "비공개 설정");
        SetText(roomCodeLabelText, "방 코드");
    }

    private void RefreshView()
    {
        SetText(maxPlayerValueText, maxPlayerCount.ToString());
        SetText(privateStateValueText, isPrivateRoom ? "비밀방" : "공개방");
        SetText(roomCodeValueText, isPrivateRoom ? roomCode : string.Empty);
        RefreshInteractable();
    }

    private void RefreshInteractable()
    {
        bool canDecreaseMaxPlayer = isHost && maxPlayerCount > minPlayerCount && maxPlayerCount > currentPlayerCount;
        bool canIncreaseMaxPlayer = isHost && maxPlayerCount < maxPlayerCountLimit;

        SetButtonInteractable(maxPlayerPrevButton, canDecreaseMaxPlayer);
        SetButtonInteractable(maxPlayerNextButton, canIncreaseMaxPlayer);
        SetButtonInteractable(privateStateToggleButton, isHost);
        SetButtonInteractable(changeRoomCodeButton, isHost && isPrivateRoom);
    }

    private void AddButtonListeners()
    {
        AddListener(maxPlayerPrevButton, OnClickMaxPlayerPrevButton);
        AddListener(maxPlayerNextButton, OnClickMaxPlayerNextButton);
        AddListener(privateStateToggleButton, OnClickPrivateStateToggleButton);
        AddListener(changeRoomCodeButton, OnClickChangeRoomCodeButton);
        AddListener(confirmRoomCodeButton, OnClickConfirmRoomCodeButton);
    }

    private void RemoveButtonListeners()
    {
        RemoveListener(maxPlayerPrevButton, OnClickMaxPlayerPrevButton);
        RemoveListener(maxPlayerNextButton, OnClickMaxPlayerNextButton);
        RemoveListener(privateStateToggleButton, OnClickPrivateStateToggleButton);
        RemoveListener(changeRoomCodeButton, OnClickChangeRoomCodeButton);
        RemoveListener(confirmRoomCodeButton, OnClickConfirmRoomCodeButton);
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static void SetObjectActive(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }
}
