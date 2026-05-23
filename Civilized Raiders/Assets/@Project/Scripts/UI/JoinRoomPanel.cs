using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class JoinRoomPanel : UIPanel
{
    [SerializeField] private UIFlowController flowController;
    [SerializeField] private Button quickJoinButton;   // 빠른 참가 (새로 추가, 자식 이름 "QuickJoin")
    [SerializeField] private Button joinButton;        // 코드 참가 (자식 이름 "Join")
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Transform roomListRoot;
    [SerializeField] private string roomLobbySceneName = "RoomLobby";

    private bool isLoadingRoom;
    private string selectedRoomCode;

    private void Awake() { AutoBind(); }
    private void OnEnable()
    {
        BindButtons();
        // 더미 방 목록 제거: Fusion 연결 전에 가짜 룸을 보여주지 않는다.
        // 5/24 M1 이후 OnSessionListUpdated로 실제 룸 목록 표시 예정.
        SetRooms(new string[0]);
        selectedRoomCode = string.Empty;
    }

    // ─── 버튼 핸들러

    /// <summary>빠른 참가 버튼 — 공개방에 바로 접속.</summary>
    public void OnQuickJoinClicked()
    {
        if (isLoadingRoom) return;
        RequestJoinPublicRoom();
    }

    /// <summary>
    /// 코드 참가 버튼.
    /// - 입력 필드에 코드가 있으면 → 비밀방 참가
    /// - 없으면 → 공개방 빠른 참가
    /// </summary>
    public void OnJoinByCodeClicked()
    {
        if (isLoadingRoom) return;

        string roomCode = roomCodeInput != null ? roomCodeInput.text.Trim() : string.Empty;

        if (!string.IsNullOrEmpty(roomCode))
        {
            RequestJoinPrivateRoom(roomCode);
            return;
        }

        if (!string.IsNullOrEmpty(selectedRoomCode))
        {
            // 방 목록에서 선택한 방 (향후 실제 룸 목록 구현 시 사용)
            RequestJoinPrivateRoom(selectedRoomCode);
            return;
        }

        // 아무것도 입력하지 않았을 때 → 빠른 참가로 처리
        RequestJoinPublicRoom();
    }

    public void OnCloseClicked()
    {
        if (flowController != null) flowController.GoBack();
    }

    public void OnRoomListItemClicked(string roomCode)
    {
        if (isLoadingRoom) return;
        selectedRoomCode = roomCode;
        Debug.Log($"[JoinRoomPanel] Selected room: {roomCode}");
    }

    // ─── 네트워크 요청

    /// <summary>공개방 빠른 참가 (PublicRoomSession에 Client로 접속)</summary>
    public void RequestJoinPublicRoom()
    {
        LobbySession.SetJoin(string.Empty); // 공개방은 코드 없음
        isLoadingRoom = true;

        FusionRoomManager.Instance.JoinQuickRoom(
            onSuccess: () =>
            {
                // 실제 접속된 세션명을 LobbySession에 기록
                LobbySession.SetJoin(FusionRoomManager.Instance.CurrentSessionName);
                OnJoinRoomSucceeded(FusionRoomManager.Instance.CurrentSessionName);
            },
            onFailed: reason =>
            {
                isLoadingRoom = false;
                Debug.LogError($"[JoinRoomPanel] QuickJoin failed: {reason}");
            }
        );
    }

    /// <summary>코드로 비밀방 참가</summary>
    public void RequestJoinPrivateRoom(string roomCode)
    {
        LobbySession.SetJoin(roomCode);
        isLoadingRoom = true;

        FusionRoomManager.Instance.JoinRoomByCode(
            roomCode: roomCode,
            onSuccess: () =>
            {
                LobbySession.SetJoin(roomCode);
                OnJoinRoomSucceeded(roomCode);
            },
            onFailed: reason =>
            {
                isLoadingRoom = false;
                Debug.LogError($"[JoinRoomPanel] JoinByCode({roomCode}) failed: {reason}");
            }
        );
    }

    // ─── 결과 처리

    public void OnJoinRoomSucceeded(string roomCode)
    {
        LobbySession.SetJoin(roomCode);
        LoadRoomLobbyScene();
    }

    public void OnJoinRoomFailed(string message)
    {
        isLoadingRoom = false;
        Debug.LogWarning($"[JoinRoomPanel] Join room failed: {message}");
    }

    // ─── 방 목록 UI (5/24 M1: OnSessionListUpdated 연동 예정)

    public void SetRooms(IReadOnlyList<string> roomCodes)
    {
        if (roomListRoot == null) return;

        for (int i = roomListRoot.childCount - 1; i >= 0; i--)
            Destroy(roomListRoot.GetChild(i).gameObject);

        foreach (string roomCode in roomCodes)
        {
            Button item = CreateRoomListButton(roomCode);
            string captured = roomCode;
            item.onClick.AddListener(() => OnRoomListItemClicked(captured));
        }

        selectedRoomCode = string.Empty;
    }

    // ─── 내부

    private void LoadRoomLobbyScene()
    {
        isLoadingRoom = true;
        SceneManager.LoadScene(roomLobbySceneName);
    }

    private Button CreateRoomListButton(string label)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(roomListRoot, false);

        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);

        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(12f, 4f);
        rt.offsetMax = new Vector2(-12f, -4f);

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;

        return go.GetComponent<Button>();
    }

    private void AutoBind()
    {
        flowController = flowController != null ? flowController : GetComponentInParent<UIFlowController>(true);
        quickJoinButton = quickJoinButton != null ? quickJoinButton : FindButton("QuickJoin");
        joinButton = joinButton != null ? joinButton : FindButton("Join");
        closeButton = closeButton != null ? closeButton : FindButton("ESC");

        Transform input = UIFlowController.FindChild(transform, "Room Code");
        roomCodeInput = roomCodeInput != null ? roomCodeInput : input != null ? input.GetComponent<TMP_InputField>() : null;

        Transform roomList = UIFlowController.FindChild(transform, "RoomList");
        roomListRoot = roomListRoot != null ? roomListRoot : roomList != null ? roomList : transform;
    }

    private void BindButtons()
    {
        AddListener(quickJoinButton, OnQuickJoinClicked);
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
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
