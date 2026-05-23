using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomPanel : UIPanel
{
    [SerializeField] private TextMeshProUGUI roomCodeText;
    [SerializeField] private Transform playerListRoot;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button hostStartButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private string titleSceneName = "Title";

    private bool isReady;

    // Fusion 이벤트 타이밍 누락 대비용 주기 갱신
    private float refreshTimer;

    // Build / Cloud Relay 환경에서 안정성 확보용
    private const float RefreshInterval = 0.5f;

    private void Awake() { AutoBind(); }

    private void OnEnable()
    {
        BindButtons();

        // 방 코드 표시 (Fusion 세션명 우선, 없으면 LobbySession fallback)
        SetRoomCode(GetDisplayRoomCode());

        // Host 여부에 따라 Start 버튼 표시/숨김
        bool isHost = FusionRoomManager.Instance != null && FusionRoomManager.Instance.IsHost;
        SetHostControlsVisible(isHost);

        // 현재 참가자 목록 초기화
        RefreshPlayerList();

        // 입퇴장 이벤트 구독
        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.OnPlayerJoinedCallback += HandlePlayerJoined;
            FusionRoomManager.Instance.OnPlayerLeftCallback += HandlePlayerLeft;
        }
    }

    private void OnDisable()
    {
        // 이벤트 해제 — 씬 전환 / 패널 비활성화 시 메모리 누수 방지
        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.OnPlayerJoinedCallback -= HandlePlayerJoined;
            FusionRoomManager.Instance.OnPlayerLeftCallback -= HandlePlayerLeft;
        }
    }
    private void Update()
    {
        // Build 환경에서 Fusion 이벤트 누락 대비
        // 일정 주기로 강제 동기화

        refreshTimer += Time.deltaTime;

        if (refreshTimer >= RefreshInterval)
        {
            refreshTimer = 0f;

            RefreshPlayerList();
        }
    }

    // ─── 버튼 핸들러

    public void OnReadyClicked()
    {
        isReady = !isReady;
        SetReadyState(isReady);
        FusionRoomManager.Instance?.Ready();
    }

    public void OnHostStartClicked()
    {
        // Host 가드는 FusionRoomManager.StartGame() 내부에서 처리
        FusionRoomManager.Instance?.StartGame();
    }

    /// <summary>
    /// Leave 버튼 → RequestLeaveRoom.
    /// ※ 이전 코드에 있던 SceneManager.LoadScene 직접 호출 제거.
    ///   씬 전환은 LeaveRoom 완료 후 ReturnToTitle 콜백에서만 발생한다.
    /// </summary>
    public void OnLeaveClicked()
    {
        RequestLeaveRoom();
    }

    /// <summary>
    /// Fusion Runner를 안전하게 종료하고 Title로 복귀.
    /// FusionRoomManager.LeaveRoom 완료 → ReturnToTitle 콜백 실행.
    /// </summary>
    public void RequestLeaveRoom()
    {
        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.LeaveRoom(ReturnToTitle);
        }
        else
        {
            // Fusion 미연결 상태 (에디터 UI 테스트 등)
            ReturnToTitle();
        }
    }

    /// <summary>LobbySession 초기화 후 Title 씬으로 복귀.</summary>
    public void ReturnToTitle()
    {
        LobbySession.Clear();
        SceneManager.LoadScene(titleSceneName);
    }

    // ─── UI 갱신

    public void SetRoomCode(string roomCode)
    {
        if (roomCodeText != null)
            roomCodeText.text = $"Room Code : {roomCode}";
    }

    public void SetHostControlsVisible(bool isHost)
    {
        if (hostStartButton != null)
            hostStartButton.gameObject.SetActive(isHost);
    }

    public void SetReadyState(bool ready)
    {
        isReady = ready;
        TextMeshProUGUI label = readyButton != null
            ? readyButton.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        if (label != null)
            label.text = ready ? "Cancel Ready" : "Ready";
    }

    public void SetPlayers(IReadOnlyList<string> players)
    {
        if (playerListRoot == null) return;

        for (int i = playerListRoot.childCount - 1; i >= 0; i--)
            Destroy(playerListRoot.GetChild(i).gameObject);

        foreach (string player in players)
            CreatePlayerText(player);
    }

    /// <summary>Fusion ActivePlayers 기준으로 참가자 목록을 갱신한다.</summary>
    public void RefreshPlayerList()
    {
        if (FusionRoomManager.Instance == null)
        {
            SetPlayers(new[] { "Player 1 (local)" });
            return;
        }

        NetworkRunner runner = FusionRoomManager.Instance.Runner;

        if (runner == null)
        {
            SetPlayers(new[] { "Waiting for players..." });
            return;
        }

        List<string> names = new List<string>();

        foreach (PlayerRef player in FusionRoomManager.Instance.GetActivePlayers())
        {
            bool isLocal = player == runner.LocalPlayer;
            // Fusion 2에는 PlayerRef.MasterClient가 없음 (PUN 개념).
            // 로컬 플레이어가 Host인 경우에만 [Host] 표시.
            // 타 클라이언트에게 Host 표시를 동기화하려면 M1에서 Networked 변수로 구현.
            string role = (isLocal && FusionRoomManager.Instance != null && FusionRoomManager.Instance.IsHost)
                ? " [Host]" : string.Empty;
            string me = isLocal ? " (You)" : string.Empty;
            names.Add($"Player {player.PlayerId}{role}{me}");
        }

        if (names.Count == 0)
        {
            names.Add("Waiting for players...");
        }

        SetPlayers(names);
    }

    // ─── 이벤트 핸들러 (FusionRoomManager 콜백)

    private void HandlePlayerJoined(PlayerRef player, NetworkRunner runner)
    {
        RefreshPlayerList();
    }

    private void HandlePlayerLeft(PlayerRef player, NetworkRunner runner)
    {
        RefreshPlayerList();
    }

    // ─── 내부

    private string GetDisplayRoomCode()
    {
        // Fusion 세션명 우선
        if (FusionRoomManager.Instance != null)
        {
            string name = FusionRoomManager.Instance.CurrentSessionName;
            if (!string.IsNullOrEmpty(name)) return name;
        }

        // Fusion 없을 때 (에디터 단독 테스트) fallback
        if (LobbySession.Mode == LobbySession.EntryMode.CreatePublic) return "PUBLIC";
        if (!string.IsNullOrEmpty(LobbySession.RoomCode)) return LobbySession.RoomCode;
        return "-";
    }

    private void CreatePlayerText(string text)
    {
        GameObject go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(playerListRoot, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 50f);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 44f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.gray;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void AutoBind()
    {
        roomCodeText = roomCodeText != null ? roomCodeText : FindText("RoomCodeText");
        playerListRoot = playerListRoot != null ? playerListRoot : FindTransform("PlayerListRoot");
        readyButton = readyButton != null ? readyButton : FindButton("ReadyButton");
        hostStartButton = hostStartButton != null ? hostStartButton : FindButton("HostStartButton");
        leaveButton = leaveButton != null ? leaveButton : FindButton("LeaveButton");
    }

    private void BindButtons()
    {
        AddListener(readyButton, OnReadyClicked);
        AddListener(hostStartButton, OnHostStartClicked);
        AddListener(leaveButton, OnLeaveClicked);
    }

    private Transform FindTransform(string childName)
        => UIFlowController.FindChild(transform, childName);

    private TextMeshProUGUI FindText(string childName)
    {
        Transform child = FindTransform(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private Button FindButton(string childName)
    {
        Transform child = FindTransform(childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
