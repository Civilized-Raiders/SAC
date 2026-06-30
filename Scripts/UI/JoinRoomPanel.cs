using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoinRoomPanel : UIPanel
{
    [Header("UI References")]
    [SerializeField] private UIFlowController flowController;
    [SerializeField] private UIAudioVolumeController audioVolumeController; // 사운드 관련 추가
    [SerializeField] private Button joinButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Transform roomListRoot;
    [SerializeField] private RoomListItemUI roomListItemPrefab;
    [SerializeField] private ScrollRect roomListScrollRect;

    [Header("Room Defaults")]
    [SerializeField] private int defaultMaxPlayerCount = 4;

    public static string PendingJoinRoomCode { get; private set; }

    private bool isLoadingRoom;
    private string selectedRoomCode;

    private void Awake()
    {
        AutoBind();
        ResolveRoomListRoot();
        ConfigureRoomListScroll();
    }

    private void OnEnable()
    {
        AutoBind(); // 사운드 관련 추가
        ResolveRoomListRoot();
        ConfigureRoomListScroll();
        BindButtons();

        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.OnJoinSucceeded += HandleJoinSucceeded;
            FusionRoomManager.Instance.OnJoinFailed += HandleJoinFailed;
            FusionRoomManager.Instance.OnSessionListReceived += HandleSessionListReceived;
            FusionRoomManager.Instance.RequestSessionList();
            return;
        }

        ShowTemporaryRooms();
    }

    private void OnDisable()
    {
        if (FusionRoomManager.Instance == null)
        {
            return;
        }

        FusionRoomManager.Instance.OnJoinSucceeded -= HandleJoinSucceeded;
        FusionRoomManager.Instance.OnJoinFailed -= HandleJoinFailed;
        FusionRoomManager.Instance.OnSessionListReceived -= HandleSessionListReceived;
    }

    public void OnJoinByCodeClicked()
    {
        PlayButtonClick(); // 사운드 관련 추가

        if (isLoadingRoom)
        {
            return;
        }

        string roomCode = roomCodeInput != null ? roomCodeInput.text.Trim() : string.Empty;

        if (!string.IsNullOrEmpty(roomCode))
        {
            RequestJoinPrivateRoom(roomCode);
            return;
        }

        if (!string.IsNullOrEmpty(selectedRoomCode))
        {
            RequestJoinPublicRoom(selectedRoomCode);
            return;
        }

        Debug.LogWarning("Select a room from the list or enter a room code.");
    }

    public void OnCloseClicked()
    {
        PlayButtonClick(); // 사운드 관련 추가

        if (flowController != null)
        {
            flowController.GoBack();
        }
    }

    public void OnRoomListItemClicked(string roomCode)
    {
        PlayButtonClick(); // 사운드 관련 추가

        if (isLoadingRoom)
        {
            return;
        }

        SelectRoom(roomCode);
    }

    public void SelectRoom(string roomCode)
    {
        selectedRoomCode = roomCode;
        Debug.Log($"Selected room: {roomCode}");
    }

    public void OnJoinRoomSucceeded(string roomCode)
    {
        PendingJoinRoomCode = roomCode;
        // ?ㅼ젣 ???대룞怨?諛??낆옣 ?뺤젙? FusionRoomManager 履??깃났 ?대깽???댄썑 ?먮쫫?먯꽌 泥섎━?⑸땲??
    }

    public void OnJoinRoomFailed(string message)
    {
        isLoadingRoom = false;
        Debug.LogWarning($"Join room failed. {message}");
    }

    public void SetRooms(IReadOnlyList<string> roomCodes)
    {
        ClearRoomList();

        if (roomCodes == null)
        {
            return;
        }

        foreach (string roomCode in roomCodes)
        {
            CreateRoomListItem(roomCode, -1, defaultMaxPlayerCount, true);
        }

        selectedRoomCode = string.Empty;
        RefreshRoomListLayout();
    }

    public void RequestJoinPublicRoom(string roomCode)
    {
        PendingJoinRoomCode = roomCode;

        if (FusionRoomManager.Instance == null)
        {
            OnJoinRoomFailed("FusionRoomManager가 씬에 없습니다.");
            UIEventManager.TriggerShowToast("FusionRoomManager가 씬에 없습니다.");
            return;
        }

        isLoadingRoom = true;
        UIEventManager.TriggerShowToast("방에 입장 중...");
        FusionRoomManager.Instance.JoinRoomByCode(roomCode);
    }

    public void RequestJoinPrivateRoom(string roomCode)
    {
        PendingJoinRoomCode = roomCode;

        if (FusionRoomManager.Instance == null)
        {
            OnJoinRoomFailed("FusionRoomManager가 씬에 없습니다.");
            UIEventManager.TriggerShowToast("FusionRoomManager가 씬에 없습니다.");
            return;
        }

        isLoadingRoom = true;
        UIEventManager.TriggerShowToast("방에 입장 중...");
        FusionRoomManager.Instance.JoinRoomByCode(roomCode);
    }

    private void HandleJoinSucceeded()
    {
        OnJoinRoomSucceeded(PendingJoinRoomCode);
    }

    private void HandleJoinFailed(string message)
    {
        OnJoinRoomFailed(message);
    }

    private void HandleSessionListReceived(List<SessionInfo> sessions)
    {
        ClearRoomList();

        if (sessions == null)
        {
            return;
        }

        for (int i = 0; i < sessions.Count; i++)
        {
            SessionInfo session = sessions[i];
            if (session == null) continue;
            if (!session.IsVisible) continue;
            if (!session.IsOpen) continue;

            int maxPlayers = session.MaxPlayers <= 0 ? defaultMaxPlayerCount : session.MaxPlayers;
            CreateRoomListItem(session.Name, session.PlayerCount, maxPlayers, session.IsVisible);
        }

        selectedRoomCode = string.Empty;
        RefreshRoomListLayout();
    }

    private void ShowTemporaryRooms()
    {
        SetRooms(new[] { "Public Room 01", "Public Room 02", "Private Room Example" });
    }

    private void ClearRoomList()
    {
        ResolveRoomListRoot();
        if (roomListRoot == null)
        {
            return;
        }

        for (int i = roomListRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(roomListRoot.GetChild(i).gameObject);
        }
    }

    private void CreateRoomListItem(string roomCode, int currentPlayerCount, int maxPlayerCount, bool isPublicRoom)
    {
        ResolveRoomListRoot();
        if (roomListRoot == null)
        {
            Debug.LogWarning("RoomListRoot媛 ?곌껐?섏? ?딆븯?듬땲?? JoinRoomPanel Inspector?먯꽌 RoomListContent瑜??곌껐?댁＜?몄슂.");
            return;
        }

        if (roomListItemPrefab == null)
        {
            Debug.LogWarning("RoomListItemPrefab???곌껐?섏? ?딆븯?듬땲?? JoinRoomPanel Inspector?먯꽌 RoomListItemPanel ?꾨━?뱀쓣 ?곌껐?댁＜?몄슂.");
            return;
        }

        RoomListItemUI item = Instantiate(roomListItemPrefab, roomListRoot);
        item.gameObject.SetActive(true);
        item.transform.SetAsLastSibling();
        item.SetRoomInfo(roomCode, currentPlayerCount, maxPlayerCount, isPublicRoom);

        // 諛?移대뱶??李멸? 踰꾪듉留??낆옣???붿껌?⑸땲?? 移대뱶 諛곌꼍 ?대┃? ?낆옣?쇰줈 泥섎━?섏? ?딆뒿?덈떎.
        item.SetJoinAction(() =>
        {
            PlayButtonClick(); // 사운드 관련 추가
            RequestJoinPublicRoom(roomCode);
        });
    }

    private void AutoBind()
    {
        flowController = flowController != null ? flowController : GetComponentInParent<UIFlowController>(true);
        audioVolumeController = audioVolumeController != null ? audioVolumeController : FindFirstObjectByType<UIAudioVolumeController>(); // 사운드 관련 추가
        joinButton = joinButton != null ? joinButton : FindButton("Join");
        closeButton = closeButton != null ? closeButton : FindButton("ESC");

        Transform input = UIFlowController.FindChild(transform, "Room Code");
        roomCodeInput = roomCodeInput != null ? roomCodeInput : input != null ? input.GetComponent<TMP_InputField>() : null;

        Transform roomList = UIFlowController.FindChild(transform, "Content");
        if (roomList == null)
        {
            roomList = UIFlowController.FindChild(transform, "RoomListContent");
        }
        roomListRoot = roomListRoot != null ? roomListRoot : roomList;
        roomListScrollRect = roomListScrollRect != null ? roomListScrollRect : roomList != null ? roomList.GetComponent<ScrollRect>() : null;
    }

    private void ResolveRoomListRoot()
    {
        roomListScrollRect = roomListScrollRect != null ? roomListScrollRect : roomListRoot != null ? roomListRoot.GetComponent<ScrollRect>() : null;

        if (roomListRoot == null)
        {
            return;
        }

        ScrollRect scrollRect = roomListScrollRect != null ? roomListScrollRect : roomListRoot.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            return;
        }

        if (scrollRect.content != null)
        {
            roomListRoot = scrollRect.content;
            return;
        }

        Transform content = UIFlowController.FindChild(roomListRoot, "Content");
        if (content != null)
        {
            roomListRoot = content;
        }
    }

    private void ConfigureRoomListScroll()
    {
        if (roomListScrollRect == null)
        {
            return;
        }

        roomListScrollRect.horizontal = false;
        roomListScrollRect.vertical = true;
        roomListScrollRect.inertia = false;
        roomListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        roomListScrollRect.elasticity = 0f;
    }

 
    private void RefreshRoomListLayout()
    {
        if (roomListRoot == null)
        {
            return;
        }

        RectTransform rect = roomListRoot as RectTransform;
        if (rect == null)
        {
            return;
        }

        // Room list cards are spawned at runtime, so we force Unity's layout system
        // to recalculate immediately instead of waiting for a later canvas pass.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        Canvas.ForceUpdateCanvases();
    }

    private void BindButtons()
    {
        AddListener(joinButton, OnJoinByCodeClicked);
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
