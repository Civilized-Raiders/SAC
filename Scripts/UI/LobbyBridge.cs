// File: Assets/@Project/Scripts/UI/LobbyBridge.cs
// Role : RoomLobby 씬 안에서 기존 UI 컴포넌트들과 네트워크(System/Contents 측)를 연결하는 어댑터.
//        UI 컴포넌트의 코드는 건드리지 않고, 이 파일이 글루(glue) 역할만 한다.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyBridge : MonoBehaviour
{
    [System.Serializable]
    private class MockPlayerPreview
    {
        public bool isOccupied = true;
        public string playerName = "Player";
        public bool isHost;
        public bool isLocal;
        public bool isReady;
        public bool isMicOn = true;
        public int colorIndex;
    }

    [Header("UI References (씬 안 객체를 Inspector에 드래그)")]
    [SerializeField] private RoomLobbyPanelUI roomLobbyPanel;
    [SerializeField] private RoomPlayerSlotUI[] playerSlots;
    [SerializeField] private RoomBottomActionUI bottomActionUI;
    [SerializeField] private RoomSettingPanelUI roomSettingPanel;
    [SerializeField] private LobbySettingPanelUI lobbySettingPanel;
    [SerializeField] private RoomColorSelectUI colorSelectUI;
    [SerializeField] private RoomMapSelectUI mapSelectUI;

    [Header("Color Mapping")]
    [Tooltip("colorIds[i] 가 colorId == i 에 대응. RoomColorSelectUI 의 colorId 문자열과 인덱스 순서를 일치시킬 것.")]
    [SerializeField] private string[] colorIds = { "Red", "Blue", "Green", "Yellow", "Purple", "Black", "White" };
    [SerializeField] private Sprite[] characterPreviewSprites;

    [Header("Start Conditions")]
    [SerializeField] private int minStartPlayerCount = 2;
    [SerializeField] private bool requireRoomFull = true;

    [Header("Test Lobby Mock")]
    [SerializeField] private bool useMockLobbyWhenNoFusion = true;
    [SerializeField]
    private MockPlayerPreview[] mockPlayers =
    {
        new MockPlayerPreview { playerName = "Player1", isHost = true, isLocal = true, isReady = false, colorIndex = 0 },
        new MockPlayerPreview { playerName = "Player2", isHost = false, isLocal = false, isReady = false, colorIndex = 1 },
        new MockPlayerPreview { isOccupied = false, playerName = "Player3", colorIndex = 2 },
        new MockPlayerPreview { isOccupied = false, playerName = "Player4", colorIndex = 3 },
    };

    private PlayerData localPlayerData;
    private bool isMockMode;
    private float nextAutoColorRequestTime;

    private void Awake()
    {
        AutoBindReferences();

        if (playerSlots != null)
        {
            for (int i = 0; i < playerSlots.Length; i++)
            {
                if (playerSlots[i] != null)
                {
                    playerSlots[i].SetEmptySlot(i + 1);
                }
            }
        }
    }

    private void OnApplicationQuit()
    {

    }

    private void Update()
    {
        if (isMockMode || FusionRoomManager.Instance == null)
        {
            return;
        }

        EnsureLocalPlayerColorSelected();
        RefreshMapSelect();
    }

    private void Start()
    {
        AutoBindReferences();

        if (FusionRoomManager.Instance != null || !useMockLobbyWhenNoFusion)
        {
            return;
        }

        isMockMode = true;
        SubscribeMockUiEvents();
        RefreshMockLobby();
    }

    private void OnEnable()
    {
        AutoBindReferences();

        if (FusionRoomManager.Instance == null)
        {
            if (useMockLobbyWhenNoFusion)
            {
                return;
            }

            Debug.LogWarning("LobbyBridge: FusionRoomManager.Instance == null. " +
                             "Title 씬에서 FusionRoomManager를 먼저 띄워야 한다.");
            return;
        }

        FusionRoomManager.Instance.OnPlayerDataSpawned += HandlePlayerDataSpawned;
        FusionRoomManager.Instance.OnPlayerDataDespawned += HandlePlayerDataDespawned;
        FusionRoomManager.Instance.OnRoomShutdown += HandleRoomShutdown;
        FusionRoomManager.Instance.OnGameStarting += HandleGameStarting;

        if (bottomActionUI != null)
        {
            bottomActionUI.LeaveRoomRequested += HandleLeaveRequested;
            bottomActionUI.ReadyStateChangeRequested += HandleReadyRequested;
            bottomActionUI.HostStartRequested += HandleHostStartRequested;
        }

        if (colorSelectUI != null)
        {
            colorSelectUI.ColorSelectRequested += HandleColorSelectRequested;
        }

        if (mapSelectUI != null)
        {
            mapSelectUI.MapSelectionChangeRequested += HandleMapSelectionChangeRequested;
        }

        // 씬 켜지기 전 이미 스폰돼 있던 PlayerData들 처리
        IReadOnlyList<PlayerData> existing = FusionRoomManager.Instance.PlayerDataList;
        for (int i = 0; i < existing.Count; i++)
        {
            HandlePlayerDataSpawned(existing[i]);
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.OnPlayerDataSpawned -= HandlePlayerDataSpawned;
            FusionRoomManager.Instance.OnPlayerDataDespawned -= HandlePlayerDataDespawned;
            FusionRoomManager.Instance.OnRoomShutdown -= HandleRoomShutdown;
            FusionRoomManager.Instance.OnGameStarting -= HandleGameStarting;

            IReadOnlyList<PlayerData> existing = FusionRoomManager.Instance.PlayerDataList;
            for (int i = 0; i < existing.Count; i++)
            {
                existing[i].Changed -= HandlePlayerDataChanged;
            }
        }

        if (bottomActionUI != null)
        {
            bottomActionUI.LeaveRoomRequested -= HandleLeaveRequested;
            bottomActionUI.ReadyStateChangeRequested -= HandleReadyRequested;
            bottomActionUI.HostStartRequested -= HandleHostStartRequested;
        }

        if (colorSelectUI != null)
        {
            colorSelectUI.ColorSelectRequested -= HandleColorSelectRequested;
        }

        if (mapSelectUI != null)
        {
            mapSelectUI.MapSelectionChangeRequested -= HandleMapSelectionChangeRequested;
        }
    }

    private void HandlePlayerDataSpawned(PlayerData data)
    {

        if (data == null) return;
        data.Changed -= HandlePlayerDataChanged;
        data.Changed += HandlePlayerDataChanged;

        // ★ localPlayerData는 반드시 내 것으로만 설정
        if (data.IsLocal)
        {
            localPlayerData = data;
            // ★ 내 데이터 확정 후에만 UI 갱신
            RefreshAll();
        }
        else
        {
            // 다른 플레이어 스폰 시엔 슬롯만 갱신
            RefreshSlots();
            RefreshRoomInfo();
        }
    }

    private void HandlePlayerDataDespawned(PlayerData data)
    {
        if (data == null) return;
        data.Changed -= HandlePlayerDataChanged;
        if (localPlayerData == data) localPlayerData = null;
        RefreshAll();
    }

    private void HandlePlayerDataChanged(PlayerData data)
    {
        RefreshAll();
    }

    private void HandleLeaveRequested()
    {
        HideLobbySettingPanelFromExternalLayer();

        if (isMockMode)
        {
            Debug.Log("LobbyBridge Mock: 나가기 요청은 테스트 모드에서 씬 이동 없이 로그만 남깁니다.");
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.LeaveRoom();
            SceneManager.LoadScene("Title");
        }
        else
        {
            SceneManager.LoadScene("Title");
        }
    }

    private void HandleReadyRequested(bool _)
    {
        HideLobbySettingPanelFromExternalLayer();

        if (isMockMode)
        {
            ToggleMockReady();
            return;
        }

        if (localPlayerData == null) return;

        bool nextReady = !(bool)localPlayerData.IsReady;
        _pendingReadyState = nextReady;
        _isOptimisticUpdate = true;

        if (bottomActionUI != null)
            bottomActionUI.ApplyLocalReadyState(nextReady);

        localPlayerData.RequestToggleReady();
    }

    private void HandleHostStartRequested()
    {
        HideLobbySettingPanelFromExternalLayer();

        if (isMockMode)
        {
            Debug.Log("LobbyBridge Mock: 게임 시작 요청. 실제 씬 이동은 FusionRoomManager 연결 후 처리합니다.");
            return;
        }

        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.RequestStartGame();
        }
    }

    private void HandleColorSelectRequested(string colorId)
    {
        HideLobbySettingPanelFromExternalLayer();

        if (isMockMode)
        {
            SetMockLocalColor(colorId);
            return;
        }

        if (localPlayerData == null) return;
        int idx = System.Array.IndexOf(colorIds, colorId);
        if (idx < 0) return;
        localPlayerData.RequestSetColor(idx);
    }

    private void HandleMapSelectionChangeRequested(int mapIndex)
    {
        HideLobbySettingPanelFromExternalLayer();

        // UI는 방장이 누른 맵 선택 요청만 알려준다.
        // 실제 선택 확정, 다른 플레이어 동기화, 게임 시작 시 씬 결정은 NET/Fusion 쪽에서 처리해야 한다.
        Debug.Log($"LobbyBridge: Map selection requested. Index={mapIndex}");
        if (mapSelectUI != null && FusionRoomManager.Instance != null)
        {
            if (localPlayerData != null && localPlayerData.IsLocalHost)
            {
                localPlayerData.RequestSetSelectedMapIndex(mapIndex);
            }

            RoomMapSelectUI.MapSlot selectedMap = mapSelectUI.GetSelectedMap();
            if (selectedMap != null && !string.IsNullOrEmpty(selectedMap.mapId))
            {
                FusionRoomManager.Instance.SetSelectedMap(selectedMap.mapId);
            }
        }
        if (isMockMode)
        {
            RefreshMockBottomButtons();
            return;
        }

        RefreshBottomButtons();
    }

    //FindObjectType 수정할것!!6에선 안쓰입니다.
    private void AutoBindReferences()
    {
        roomLobbyPanel = IsUsable(roomLobbyPanel) ? roomLobbyPanel : FindFirstObjectByType<RoomLobbyPanelUI>(FindObjectsInactive.Include);
        bottomActionUI = IsUsable(bottomActionUI) ? bottomActionUI : FindFirstObjectByType<RoomBottomActionUI>(FindObjectsInactive.Include);
        roomSettingPanel = IsUsable(roomSettingPanel) ? roomSettingPanel : FindFirstObjectByType<RoomSettingPanelUI>(FindObjectsInactive.Include);
        lobbySettingPanel = IsUsable(lobbySettingPanel) ? lobbySettingPanel : FindFirstObjectByType<LobbySettingPanelUI>(FindObjectsInactive.Include);
        colorSelectUI = IsUsable(colorSelectUI) ? colorSelectUI : FindFirstObjectByType<RoomColorSelectUI>(FindObjectsInactive.Include);
        mapSelectUI = IsUsable(mapSelectUI) ? mapSelectUI : FindFirstObjectByType<RoomMapSelectUI>(FindObjectsInactive.Include);

        if (playerSlots == null || playerSlots.Length == 0 || !HasUsableSlots(playerSlots))
        {
            playerSlots = FindObjectsByType<RoomPlayerSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }

    private static bool IsUsable(Component component)
    {
        return component != null;
    }

    private void HideLobbySettingPanelFromExternalLayer()
    {
        if (lobbySettingPanel != null && lobbySettingPanel.gameObject.activeSelf)
        {
            lobbySettingPanel.HideFromExternalLayer();
        }
    }

    private static bool HasUsableSlots(RoomPlayerSlotUI[] slots)
    {
        if (slots == null || slots.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshAll()
    {
        if (FusionRoomManager.Instance == null) return;

        EnsureLocalPlayerColorSelected();
        RefreshSlots();
        RefreshRoomInfo();
        RefreshBottomButtons();
        RefreshColorSelect();
        RefreshMapSelect();
    }

    private void RefreshSlots()
    {
        if (playerSlots == null) return;

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null) playerSlots[i].SetEmptySlot(i + 1);
        }

        List<PlayerData> list = new List<PlayerData>(FusionRoomManager.Instance.PlayerDataList);

        // ★ PlayerId 대신 리스트 순서(i)를 슬롯 인덱스로 사용
        for (int i = 0; i < list.Count && i < playerSlots.Length; i++)
        {
            PlayerData data = list[i];
            if (data == null) continue;

            RoomPlayerSlotUI slot = playerSlots[i];
            if (slot == null) continue;

            string name = data.Nickname.ToString();
            if (string.IsNullOrEmpty(name)) name = $"Player{data.PlayerRef.PlayerId}";
            if (data.IsLocal) name = $"{name} {{ME}}";

            slot.SetSlotNumber(i + 1);  // ★ PlayerId 아닌 순서 번호
            slot.SetPlayerInfo(name, data.IsRoomHostPlayer, data.IsLocal);
            slot.SetReadyState((bool)data.IsReady);
            slot.SetMicState(true);
            slot.SetCharacterPreview(GetPlayerCharacterSprite(data.ColorId), GetPlayerCharacterColor(data.ColorId));
        }
    }

    private void RefreshRoomInfo()
    {
        if (FusionRoomManager.Instance == null) return;
        FusionRoomManager.RoomInfo info = FusionRoomManager.Instance.CurrentRoomInfo;
        int count = FusionRoomManager.Instance.PlayerDataList.Count;
        int maxPlayers = info.MaxPlayers <= 0 ? 4 : info.MaxPlayers;

        if (roomLobbyPanel != null)
        {
            roomLobbyPanel.SetRoomInfo(
                string.IsNullOrEmpty(info.RoomName) ? "ROOM" : info.RoomName,
                info.IsPrivate,
                count,
                maxPlayers);
        }

        if (roomSettingPanel != null)
        {
            bool localIsHost = IsLocalHostController();
            roomSettingPanel.SetHostControl(localIsHost);
            roomSettingPanel.SetRoomSettingInfo(count, maxPlayers, info.IsPrivate, info.RoomCode);
        }

        if (mapSelectUI != null)
        {
            bool localIsHost = IsLocalHostController();
            mapSelectUI.SetHostControl(localIsHost);
        }
    }



    private bool _pendingReadyState = false;
    private bool _isOptimisticUpdate = false;
    private void RefreshBottomButtons()
    {
        if (bottomActionUI == null) return;
        if (localPlayerData == null) return;

        bool localIsHost = localPlayerData != null && localPlayerData.IsLocalHost;
        bottomActionUI.SetHostState(localIsHost);

        if (localPlayerData != null)
        {
            // ★ RPC 응답이 오면 낙관적 업데이트 해제
            if (_isOptimisticUpdate && (bool)localPlayerData.IsReady == _pendingReadyState)
                _isOptimisticUpdate = false;

            // ★ 낙관적 업데이트 중이면 덮어쓰지 않음
            if (!_isOptimisticUpdate)
                bottomActionUI.ApplyLocalReadyState((bool)localPlayerData.IsReady);
        }

        bottomActionUI.SetStartAvailability(CanStartGame(localIsHost));
    }

    private bool CanStartGame(bool localIsHost)
    {
        if (!localIsHost) return false;
        if (localPlayerData == null || !(bool)localPlayerData.IsReady) return false;
        if (FusionRoomManager.Instance == null) return false;

        IReadOnlyList<PlayerData> all = FusionRoomManager.Instance.PlayerDataList;
        int count = all.Count;
        if (count < minStartPlayerCount) return false;

        for (int i = 0; i < count; i++)
        {
            PlayerData d = all[i];
            if (d == null || !(bool)d.IsReady) return false;
        }

        // 선택된 맵이 아직 준비되지 않은 슬롯이면 시작 버튼이 활성화되지 않도록 한다.
        // 맵 선택의 최종 확정과 씬 이동은 FusionRoomManager 쪽에서 받아 처리하는 구조가 안전하다.
        if (mapSelectUI != null && !mapSelectUI.HasPlayableSelectedMap) return false;

        if (requireRoomFull)
        {
            int maxPlayers = FusionRoomManager.Instance.CurrentRoomInfo.MaxPlayers;
            if (maxPlayers <= 0) maxPlayers = playerSlots != null ? playerSlots.Length : count;
            if (count < maxPlayers) return false;
        }

        return true;
    }


    private void RefreshColorSelect()
    {
        if (colorSelectUI == null) return;

        if (localPlayerData != null && localPlayerData.ColorId >= 0 && localPlayerData.ColorId < colorIds.Length)
        {
            colorSelectUI.SetSelectedColor(colorIds[localPlayerData.ColorId]);
        }
        else
        {
            colorSelectUI.SetSelectedColor(string.Empty);
        }

        List<string> locked = new List<string>();
        IReadOnlyList<PlayerData> all = FusionRoomManager.Instance.PlayerDataList;
        for (int i = 0; i < all.Count; i++)
        {
            PlayerData d = all[i];
            if (d == null) continue;
            if (localPlayerData != null && d == localPlayerData) continue;
            if (d.ColorId < 0 || d.ColorId >= colorIds.Length) continue;
            locked.Add(colorIds[d.ColorId]);
        }
        colorSelectUI.SetLockedColors(locked.ToArray());
    }

    private void RefreshMapSelect()
    {
        if (mapSelectUI == null) return;

        bool localIsHost = IsLocalHostController();
        mapSelectUI.SetHostControl(localIsHost);

        if (!localIsHost)
        {
            int syncedMapIndex = GetSyncedMapIndex();
            if (syncedMapIndex >= 0)
            {
                mapSelectUI.ApplySyncedMapIndex(syncedMapIndex);
            }
            else if (FusionRoomManager.Instance != null && !string.IsNullOrEmpty(FusionRoomManager.Instance.SelectedMapId))
            {
                mapSelectUI.ApplySyncedMapId(FusionRoomManager.Instance.SelectedMapId);
            }
        }

        if (FusionRoomManager.Instance != null)
        {
            RoomMapSelectUI.MapSlot selectedMap = mapSelectUI.GetSelectedMap();
            if (selectedMap != null && !string.IsNullOrEmpty(selectedMap.mapId))
            {
                FusionRoomManager.Instance.SetSelectedMap(selectedMap.mapId);
            }
        }
    }

    private void SubscribeMockUiEvents()
    {
        if (bottomActionUI != null)
        {
            bottomActionUI.LeaveRoomRequested -= HandleLeaveRequested;
            bottomActionUI.ReadyStateChangeRequested -= HandleReadyRequested;
            bottomActionUI.HostStartRequested -= HandleHostStartRequested;
            bottomActionUI.LeaveRoomRequested += HandleLeaveRequested;
            bottomActionUI.ReadyStateChangeRequested += HandleReadyRequested;
            bottomActionUI.HostStartRequested += HandleHostStartRequested;
        }

        if (colorSelectUI != null)
        {
            colorSelectUI.ColorSelectRequested -= HandleColorSelectRequested;
            colorSelectUI.ColorSelectRequested += HandleColorSelectRequested;
        }

        if (mapSelectUI != null)
        {
            mapSelectUI.MapSelectionChangeRequested -= HandleMapSelectionChangeRequested;
            mapSelectUI.MapSelectionChangeRequested += HandleMapSelectionChangeRequested;
        }
    }

    private void RefreshMockLobby()
    {
        // Test_Lobby 단독 실행에서 UI 배치와 버튼 상태를 확인하기 위한 임시 표시 모드다.
        // 실제 방장/플레이어/색상/맵 확정은 FusionRoomManager와 PlayerData가 결정해야 한다.
        RefreshMockSlots();
        RefreshMockRoomInfo();
        RefreshMockColorSelect();
        RefreshMockMapSelect();
        RefreshMockBottomButtons();
    }

    private void RefreshMockSlots()
    {
        if (playerSlots == null) return;

        for (int i = 0; i < playerSlots.Length; i++)
        {
            RoomPlayerSlotUI slot = playerSlots[i];
            if (slot == null) continue;

            MockPlayerPreview mock = mockPlayers != null && i < mockPlayers.Length ? mockPlayers[i] : null;
            if (mock == null || !mock.isOccupied)
            {
                slot.SetEmptySlot(i + 1);
                continue;
            }

            slot.SetSlotNumber(i + 1);
            slot.SetPlayerInfo(mock.playerName, mock.isHost, mock.isLocal);
            slot.SetReadyState(mock.isReady);
            slot.SetMicState(mock.isMicOn);
            slot.SetCharacterPreview(GetMockCharacterSprite(mock.colorIndex), GetMockCharacterColor(mock.colorIndex));
        }
    }

    private void RefreshMockRoomInfo()
    {
        int occupiedCount = GetMockOccupiedCount();
        int maxPlayers = playerSlots != null && playerSlots.Length > 0 ? playerSlots.Length : 4;

        if (roomLobbyPanel != null)
        {
            roomLobbyPanel.SetRoomInfo("TEST_ROOM", false, occupiedCount, maxPlayers);
        }

        if (roomSettingPanel != null)
        {
            roomSettingPanel.SetHostControl(true);
            roomSettingPanel.SetRoomSettingInfo(occupiedCount, maxPlayers, false, "TEST");
        }
    }

    private void RefreshMockColorSelect()
    {
        if (colorSelectUI == null) return;

        int localColorIndex = GetMockLocalColorIndex();
        colorSelectUI.SetSelectedColor(GetColorId(localColorIndex));

        List<string> locked = new List<string>();
        if (mockPlayers != null)
        {
            for (int i = 0; i < mockPlayers.Length; i++)
            {
                MockPlayerPreview mock = mockPlayers[i];
                if (mock == null || !mock.isOccupied || mock.isLocal) continue;
                string colorId = GetColorId(mock.colorIndex);
                if (!string.IsNullOrEmpty(colorId)) locked.Add(colorId);
            }
        }

        colorSelectUI.SetLockedColors(locked.ToArray());
    }

    private void RefreshMockMapSelect()
    {
        if (mapSelectUI != null)
        {
            mapSelectUI.SetHostControl(true);
        }
    }

    private void RefreshMockBottomButtons()
    {
        if (bottomActionUI == null) return;

        MockPlayerPreview localMock = GetMockLocalPlayer();
        bool localReady = localMock != null && localMock.isReady;

        bottomActionUI.SetHostState(true);
        bottomActionUI.ApplyLocalReadyState(localReady);
        bottomActionUI.SetStartAvailability(CanMockStartGame());
    }

    private bool CanMockStartGame()
    {
        if (GetMockOccupiedCount() < minStartPlayerCount) return false;
        if (mapSelectUI != null && !mapSelectUI.HasPlayableSelectedMap) return false;

        if (mockPlayers == null) return false;
        for (int i = 0; i < mockPlayers.Length; i++)
        {
            MockPlayerPreview mock = mockPlayers[i];
            if (mock == null || !mock.isOccupied) continue;
            if (!mock.isReady) return false;
        }

        return true;
    }

    private void ToggleMockReady()
    {
        MockPlayerPreview localMock = GetMockLocalPlayer();
        if (localMock == null) return;

        localMock.isReady = !localMock.isReady;
        RefreshMockLobby();
    }

    private void SetMockLocalColor(string colorId)
    {
        MockPlayerPreview localMock = GetMockLocalPlayer();
        if (localMock == null) return;

        int colorIndex = System.Array.IndexOf(colorIds, colorId);
        if (colorIndex < 0) return;

        localMock.colorIndex = colorIndex;
        RefreshMockLobby();
    }

    private MockPlayerPreview GetMockLocalPlayer()
    {
        if (mockPlayers == null) return null;

        for (int i = 0; i < mockPlayers.Length; i++)
        {
            MockPlayerPreview mock = mockPlayers[i];
            if (mock != null && mock.isOccupied && mock.isLocal) return mock;
        }

        return null;
    }

    private int GetMockOccupiedCount()
    {
        int count = 0;
        if (mockPlayers == null) return count;

        for (int i = 0; i < mockPlayers.Length; i++)
        {
            MockPlayerPreview mock = mockPlayers[i];
            if (mock != null && mock.isOccupied) count++;
        }

        return count;
    }

    private int GetMockLocalColorIndex()
    {
        MockPlayerPreview localMock = GetMockLocalPlayer();
        return localMock != null ? localMock.colorIndex : -1;
    }

    private string GetColorId(int colorIndex)
    {
        if (colorIds == null || colorIndex < 0 || colorIndex >= colorIds.Length)
        {
            return string.Empty;
        }

        return colorIds[colorIndex];
    }

    private void EnsureLocalPlayerColorSelected()
    {
        if (localPlayerData == null || localPlayerData.ColorId >= 0 || colorIds == null || colorIds.Length == 0)
        {
            return;
        }

        if (Time.unscaledTime < nextAutoColorRequestTime)
        {
            return;
        }

        int colorIndex = FindFirstAvailableColorIndex();
        if (colorIndex < 0)
        {
            return;
        }

        nextAutoColorRequestTime = Time.unscaledTime + 0.5f;
        localPlayerData.RequestSetColor(colorIndex);
    }

    private int FindFirstAvailableColorIndex()
    {
        if (FusionRoomManager.Instance == null || colorIds == null || localPlayerData == null)
        {
            return -1;
        }

        for (int colorIndex = 0; colorIndex < colorIds.Length; colorIndex++)
        {
            if (!FusionRoomManager.Instance.IsColorTaken(colorIndex, localPlayerData.PlayerRef))
            {
                return colorIndex;
            }
        }

        return -1;
    }

    private bool IsLocalHostController()
    {
        if (localPlayerData != null)
        {
            return localPlayerData.IsLocalHost;
        }

        return FusionRoomManager.Instance != null &&
               FusionRoomManager.Instance.Runner != null &&
               FusionRoomManager.Instance.Runner.IsServer;
    }

    private int GetSyncedMapIndex()
    {
        if (FusionRoomManager.Instance == null)
        {
            return -1;
        }

        IReadOnlyList<PlayerData> all = FusionRoomManager.Instance.PlayerDataList;
        PlayerData fallbackData = null;
        int fallbackPlayerId = int.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            PlayerData data = all[i];
            if (data == null)
            {
                continue;
            }

            if (data.IsRoomHostPlayer)
            {
                return data.SelectedMapIndex;
            }

            int playerId = data.PlayerRef.PlayerId;
            if (playerId > 0 && playerId < fallbackPlayerId)
            {
                fallbackPlayerId = playerId;
                fallbackData = data;
            }
        }

        if (fallbackData != null)
        {
            return fallbackData.SelectedMapIndex;
        }

        return -1;
    }

    private Sprite GetMockCharacterSprite(int colorIndex)
    {
        if (characterPreviewSprites == null || colorIndex < 0 || colorIndex >= characterPreviewSprites.Length)
        {
            return null;
        }

        return characterPreviewSprites[colorIndex];
    }

    private Color GetMockCharacterColor(int colorIndex)
    {
        switch (colorIndex)
        {
            case 0: return Color.red;
            case 1: return Color.blue;
            case 2: return Color.green;
            case 3: return Color.yellow;
            case 4: return new Color(0.55f, 0f, 0.8f);
            case 5: return Color.black;
            case 6: return Color.white;
            default: return Color.white;
        }
    }

    private Sprite GetPlayerCharacterSprite(int colorIndex)
    {
        if (characterPreviewSprites == null || colorIndex < 0 || colorIndex >= characterPreviewSprites.Length)
        {
            return null;
        }

        return characterPreviewSprites[colorIndex];
    }

    private Color GetPlayerCharacterColor(int colorIndex)
    {
        return GetPlayerCharacterSprite(colorIndex) != null ? Color.white : GetMockCharacterColor(colorIndex);
    }

    private void HandleRoomShutdown()
    {
        localPlayerData = null;
    }

    private void HandleGameStarting()
    {
        if (bottomActionUI != null)
            bottomActionUI.SetInteractable(false);
    }

}
