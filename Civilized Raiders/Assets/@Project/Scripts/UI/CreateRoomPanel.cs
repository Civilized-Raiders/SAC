using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CreateRoomPanel : UIPanel
{
    [SerializeField] private UIFlowController flowController;
    [SerializeField] private Button publicRoomButton;
    [SerializeField] private Button privateRoomButton;
    [SerializeField] private Button privateRoomConfirmButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_InputField privateRoomInput;
    [SerializeField] private string roomLobbySceneName = "RoomLobby";

    private bool isLoadingRoom;

    private void Awake() { AutoBind(); }
    private void OnEnable() { BindButtons(); }

    // ─── 버튼 핸들러

    public void OnCreatePublicRoomClicked()
    {
        if (isLoadingRoom) return;
        RequestCreatePublicRoom();
    }

    public void OnCreatePrivateRoomClicked()
    {
        if (privateRoomInput != null)
        {
            privateRoomInput.gameObject.SetActive(true);
            privateRoomInput.Select();
        }
    }

    public void OnConfirmPrivateRoomClicked()
    {
        if (isLoadingRoom) return;
        string roomCode = privateRoomInput != null ? privateRoomInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogWarning("[CreateRoomPanel] Private room code is empty.");
            return;
        }
        RequestCreatePrivateRoom(roomCode);
    }

    public void OnCloseClicked()
    {
        if (flowController != null) flowController.GoBack();
    }

    #region 네트워크 요청

    public void RequestCreatePublicRoom()
    {
        LobbySession.SetCreate(string.Empty, false);
        isLoadingRoom = true;

        FusionRoomManager.Instance.CreatePublicRoom(
            onSuccess: () => OnCreateRoomSucceeded(string.Empty, false),
            onFailed: reason =>
            {
                isLoadingRoom = false;
                Debug.LogError($"[CreateRoomPanel] CreatePublicRoom failed: {reason}");
            }
        );
    }

    public void RequestCreatePrivateRoom(string roomCode)
    {
        LobbySession.SetCreate(roomCode, true);
        isLoadingRoom = true;

        FusionRoomManager.Instance.CreatePrivateRoom(
            roomCode: roomCode,
            onSuccess: () => OnCreateRoomSucceeded(roomCode, true),
            onFailed: reason =>
            {
                isLoadingRoom = false;
                Debug.LogError($"[CreateRoomPanel] CreatePrivateRoom failed: {reason}");
            }
        );
    }
    #endregion


    // ─── 결과 처리

    public void OnCreateRoomSucceeded(string roomCode, bool isPrivateRoom)
    {
        LobbySession.SetCreate(roomCode, isPrivateRoom);
        // Host도 Client(JoinRoomPanel)와 동일하게 SceneManager.LoadScene으로 직접 전환.
        // runner.LoadScene + NetworkSceneManagerDefault 혼용 시
        // Fusion이 씬 불일치를 감지해 DisconnectByServerTimeout이 발생하므로 제거.
        SceneManager.LoadScene(roomLobbySceneName);
    }

    public void OnCreateRoomFailed(string message)
    {
        isLoadingRoom = false;
        Debug.LogWarning($"[CreateRoomPanel] Create room failed: {message}");
    }


    private void AutoBind()
    {
        flowController = flowController != null ? flowController : GetComponentInParent<UIFlowController>(true);
        publicRoomButton = publicRoomButton != null ? publicRoomButton : FindButton("PublicRoom");
        privateRoomButton = privateRoomButton != null ? privateRoomButton : FindButton("PrivateRoom");
        privateRoomConfirmButton = privateRoomConfirmButton != null ? privateRoomConfirmButton : FindButton("PrivateRoomConfirm");
        closeButton = closeButton != null ? closeButton : FindButton("ESC");

        Transform input = UIFlowController.FindChild(transform, "PrivateRoomInput");
        privateRoomInput = privateRoomInput != null ? privateRoomInput : input != null ? input.GetComponent<TMP_InputField>() : null;
    }

    private void BindButtons()
    {
        AddListener(publicRoomButton, OnCreatePublicRoomClicked);
        AddListener(privateRoomButton, OnCreatePrivateRoomClicked);
        AddListener(privateRoomConfirmButton, OnConfirmPrivateRoomClicked);
        AddListener(closeButton, OnCloseClicked);
    }

    private Button FindButton(string childName)
    {
        Transform child = UIFlowController.FindChild(transform, childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
