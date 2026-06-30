// File: Assets/@Project/Scripts/UI/CreateRoomPanel.cs
// Modified: TODO Fusion 자리를 FusionRoomManager 호출로 치환. LoadRoomLobbyScene 제거.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomPanel : UIPanel
{
    [SerializeField] private UIFlowController flowController;
    [SerializeField] private UIAudioVolumeController audioVolumeController; // 사운드 관련 추가
    [SerializeField] private Button publicRoomButton;
    [SerializeField] private Button privateRoomButton;
    [SerializeField] private Button privateRoomConfirmButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_InputField privateRoomInput;

    public static string PendingRoomCode { get; private set; }
    public static bool PendingRoomIsPrivate { get; private set; }

    private bool isLoadingRoom;

    private void Awake()
    {
        AutoBind();
    }

    private void OnEnable()
    {
        AutoBind(); // 사운드 관련 추가
        BindButtons();

        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.OnJoinSucceeded += HandleJoinSucceeded;
            FusionRoomManager.Instance.OnJoinFailed += HandleJoinFailed;
        }
    }

    private void OnDisable()
    {
        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.OnJoinSucceeded -= HandleJoinSucceeded;
            FusionRoomManager.Instance.OnJoinFailed -= HandleJoinFailed;
        }
    }

    public void OnCreatePublicRoomClicked()
    {
        PlayButtonClick(); // 사운드 관련 추가

        if (isLoadingRoom)
        {
            return;
        }

        RequestCreatePublicRoom();
    }

    public void OnCreatePrivateRoomClicked()
    {
        PlayButtonClick(); // 사운드 관련 추가

        if (privateRoomInput != null)
        {
            privateRoomInput.gameObject.SetActive(true);
            privateRoomInput.Select();
        }
    }

    public void OnConfirmPrivateRoomClicked()
    {
        PlayButtonClick(); // 사운드 관련 추가

        if (isLoadingRoom)
        {
            return;
        }

        string roomCode = privateRoomInput != null ? privateRoomInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogWarning("Private room code is empty.");
            return;
        }

        RequestCreatePrivateRoom(roomCode);
    }

    public void OnCreateRoomSucceeded(string roomCode, bool isPrivateRoom)
    {
        PendingRoomCode = roomCode;
        PendingRoomIsPrivate = isPrivateRoom;
        // NOTE: 씬 이동은 FusionRoomManager가 StartGameArgs.Scene으로 처리한다.
        //       여기서 직접 LoadScene을 호출하면 Fusion 씬 동기화와 충돌하므로 호출하지 않는다.
    }

    public void OnCreateRoomFailed(string message)
    {
        isLoadingRoom = false;
        Debug.LogWarning($"Create room failed. {message}");
    }

    public void OnCloseClicked()
    {
        PlayButtonClick(); // 사운드 관련 추가

        if (flowController != null)
        {
            flowController.GoBack();
        }
    }

    public void RequestCreatePublicRoom()
    {
        PendingRoomCode = string.Empty;
        PendingRoomIsPrivate = false;

        if (FusionRoomManager.Instance == null)
        {
            OnCreateRoomFailed("FusionRoomManager가 씬에 없습니다. Title 씬에 추가하세요.");
            return;
        }

        isLoadingRoom = true;
        FusionRoomManager.Instance.CreatePublicRoom();
    }

    public void RequestCreatePrivateRoom(string roomCode)
    {
        PendingRoomCode = roomCode;
        PendingRoomIsPrivate = true;

        if (FusionRoomManager.Instance == null)
        {
            OnCreateRoomFailed("FusionRoomManager가 씬에 없습니다.");
            return;
        }

        isLoadingRoom = true;
        FusionRoomManager.Instance.CreatePrivateRoom(roomCode);
    }

    private void HandleJoinSucceeded()
    {
        OnCreateRoomSucceeded(PendingRoomCode, PendingRoomIsPrivate);
    }

    private void HandleJoinFailed(string message)
    {
        OnCreateRoomFailed(message);
    }

    private void AutoBind()
    {
        flowController = flowController != null ? flowController : GetComponentInParent<UIFlowController>(true);
        audioVolumeController = audioVolumeController != null ? audioVolumeController : FindFirstObjectByType<UIAudioVolumeController>(); // 사운드 관련 추가
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
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
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