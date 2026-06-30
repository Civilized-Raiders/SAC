// File: Assets/@Project/Scripts/System/FusionRoomManager.cs
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionRoomManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Serializable]
    private struct MapSceneBinding
    {
        public string mapId;
        public string sceneName;
    }

    public static FusionRoomManager Instance { get; private set; }

    [Header("Network")]
    [Tooltip("PlayerData 컴포넌트가 붙은 NetworkObject 프리팹 (Inspector에서 할당 필수)")]
    [SerializeField] private NetworkObject playerDataPrefab;

    //  StateManager가 가져가서 쓸 수 있도록 프로퍼티 개방 (인스펙터 할당 중복 방지)
    public NetworkObject PlayerDataPrefab => playerDataPrefab;

    [Header("Scene Names / Build Indices")]
    //[SerializeField] private string titleSceneName = "Title";
    [SerializeField] private int roomLobbySceneBuildIndex = 1;
    [SerializeField] private int gameSceneBuildIndex = -1;

    [Header("Room Defaults")]
    [SerializeField] private int defaultMaxPlayers = 4;

    [Header("Map Scene Bindings")]
    [SerializeField]
    private MapSceneBinding[] mapSceneBindings =
    {
        new MapSceneBinding { mapId = "Ruined_City", sceneName = "Chinese_Alley" },
    };

    public event Action OnJoinSucceeded;
    public event Action<string> OnJoinFailed;
    public event Action OnRoomShutdown;
    public event Action<PlayerData> OnPlayerDataSpawned;
    public event Action<PlayerData> OnPlayerDataDespawned;
    public event Action<List<SessionInfo>> OnSessionListReceived;
    public event Action OnGameStarting;

    public struct RoomInfo
    {
        public string RoomName;
        public string RoomCode;
        public bool IsPrivate;
        public int MaxPlayers;
    }

    public RoomInfo CurrentRoomInfo { get; private set; }
    public NetworkRunner Runner => runner;
    public IReadOnlyList<PlayerData> PlayerDataList => playerDataList;
    public string SelectedMapId => selectedMapId;

    private NetworkRunner runner;
    private readonly Dictionary<PlayerRef, PlayerData> playerDataMap = new Dictionary<PlayerRef, PlayerData>();
    private readonly List<PlayerData> playerDataList = new List<PlayerData>();
    private List<SessionInfo> latestSessionList;
    private bool isStartingOrJoining;
    private string selectedMapId = "Ruined_City";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsColorTaken(int colorId, PlayerRef exceptPlayer)
    {
        if (colorId < 0) return false;
        for (int i = 0; i < playerDataList.Count; i++)
        {
            PlayerData data = playerDataList[i];
            if (data == null) continue;
            if (data.PlayerRef == exceptPlayer) continue;
            if (data.ColorId == colorId) return true;
        }
        return false;
    }

    public void RegisterPlayerData(PlayerData data)
    {
        if (data == null) return;
        PlayerRef key = data.PlayerRef;
        if (!playerDataMap.ContainsKey(key))
        {
            playerDataMap[key] = data;
            playerDataList.Add(data);
            OnPlayerDataSpawned?.Invoke(data);
        }
    }

    public void UnregisterPlayerData(PlayerData data)
    {
        if (data == null) return;
        PlayerRef key = data.PlayerRef;
        if (playerDataMap.Remove(key))
        {
            playerDataList.Remove(data);
            OnPlayerDataDespawned?.Invoke(data);
        }
    }

    public PlayerData GetLocalPlayerData()
    {
        if (runner == null) return null;
        if (playerDataMap.TryGetValue(runner.LocalPlayer, out PlayerData data)) return data;
        return null;
    }

    public PlayerData GetPlayerData(PlayerRef player)
       => playerDataMap.TryGetValue(player, out PlayerData data) ? data : null;

    public async void CreatePublicRoom()
    {
        if (isStartingOrJoining) return;
        UIEventManager.TriggerShowToast("방을 생성 중...");
        string code = GenerateRoomCode();
        await StartRoomInternal(GameMode.Host, code, isPrivate: false);
    }

    public async void CreatePrivateRoom(string roomCode)
    {
        if (isStartingOrJoining) return;
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            OnJoinFailed?.Invoke("방 코드가 비어 있습니다.");
            UIEventManager.TriggerShowToast("방 코드가 비어 있습니다.");
            return;
        }
        UIEventManager.TriggerShowToast("방을 생성 중...");
        await StartRoomInternal(GameMode.Host, roomCode.Trim(), isPrivate: true);
    }

    public async void JoinRoomByCode(string roomCode)
    {
        if (isStartingOrJoining) return;
        if (string.IsNullOrWhiteSpace(roomCode)) { OnJoinFailed?.Invoke("방 코드가 비어 있습니다."); return; }
        await StartRoomInternal(GameMode.Client, roomCode.Trim(), isPrivate: false);
    }

    public async void QuickJoinPublicRoom()
    {
        if (isStartingOrJoining) return;
        if (latestSessionList == null || latestSessionList.Count == 0) { OnJoinFailed?.Invoke("No joinable public room was found."); return; }

        SessionInfo best = null;
        for (int i = 0; i < latestSessionList.Count; i++)
        {
            SessionInfo s = latestSessionList[i];
            if (s == null || !s.IsVisible || !s.IsOpen || s.PlayerCount >= s.MaxPlayers) continue;
            best = s;
            break;
        }
        if (best == null) { OnJoinFailed?.Invoke("No joinable public room was found."); return; }
        await StartRoomInternal(GameMode.Client, best.Name, isPrivate: false);
    }

    public async void RequestSessionList()
    {
        await EnsureCleanRunnerAsync();
        runner = CreateRunner(isLobbyOnly: true);
        var result = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!result.Ok)
        {
            OnJoinFailed?.Invoke($"Failed to load room list: {result.ShutdownReason}");
            await EnsureCleanRunnerAsync();
        }
    }

    public void LeaveRoom()
    {
        if (runner == null) return;
        _ = runner.Shutdown();
    }

    public void SetSelectedMap(string mapId)
    {
        if (!string.IsNullOrWhiteSpace(mapId)) selectedMapId = mapId.Trim();
    }

    public void RequestStartGame()
    {
        if (runner == null || !runner.IsServer) return;

        if (playerDataList.Count == 0) return;
        for (int i = 0; i < playerDataList.Count; i++)
        {
            if (playerDataList[i] == null || !(bool)playerDataList[i].IsReady) return;
        }
        int resolvedGameSceneBuildIndex = ResolveSelectedGameSceneBuildIndex();
        if (resolvedGameSceneBuildIndex < 0) return;

        runner.LoadScene(SceneRef.FromIndex(resolvedGameSceneBuildIndex), LoadSceneMode.Single);
    }

    private async Task StartRoomInternal(GameMode mode, string sessionName, bool isPrivate)
    {
        isStartingOrJoining = true;
        if (mode == GameMode.Host)
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            if (roomLobbySceneBuildIndex < 0 || roomLobbySceneBuildIndex >= sceneCount)
            {
                isStartingOrJoining = false;
                OnJoinFailed?.Invoke("RoomLobby Build Index 오류");
                return;
            }
        }

        await EnsureCleanRunnerAsync();
        runner = CreateRunner(isLobbyOnly: false);
        NetworkSceneManagerDefault sceneManager = runner.GetComponent<NetworkSceneManagerDefault>() ?? runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        try
        {
            sceneManager.IsSceneTakeOverEnabled = true;
        }
        catch (Exception e) { Debug.LogWarning($"Scene setup fail: {e.Message}"); }

        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
        if (mode == GameMode.Host) sceneInfo.AddSceneRef(SceneRef.FromIndex(roomLobbySceneBuildIndex), LoadSceneMode.Single);

        StartGameArgs args = new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            PlayerCount = defaultMaxPlayers,
            IsVisible = !isPrivate,
            IsOpen = true,
            SceneManager = sceneManager,
            Scene = sceneInfo
        };

        StartGameResult result = await runner.StartGame(args);
        if (!result.Ok)
        {
            isStartingOrJoining = false;
            OnJoinFailed?.Invoke(result.ShutdownReason.ToString());
            await EnsureCleanRunnerAsync();
            return;
        }

        CurrentRoomInfo = new RoomInfo { RoomName = sessionName, RoomCode = sessionName, IsPrivate = isPrivate, MaxPlayers = defaultMaxPlayers };
        if (string.IsNullOrEmpty(selectedMapId)) selectedMapId = "Ruined_City";
        isStartingOrJoining = false;
        OnJoinSucceeded?.Invoke();
    }

    private NetworkRunner CreateRunner(bool isLobbyOnly)
    {
        GameObject go = new GameObject(isLobbyOnly ? "LobbyRunner" : "NetworkRunner");
        DontDestroyOnLoad(go);
        NetworkRunner nr = go.AddComponent<NetworkRunner>();
        nr.ProvideInput = !isLobbyOnly;
        go.AddComponent<NetworkSceneManagerDefault>();
        nr.AddCallbacks(this);
        return nr;
    }

    private async Task EnsureCleanRunnerAsync()
    {
        if (runner == null) { playerDataMap.Clear(); playerDataList.Clear(); return; }
        NetworkRunner toShutdown = runner; runner = null;
        try
        {
            if (toShutdown.IsRunning || toShutdown.State != NetworkRunner.States.Shutdown)
                await toShutdown.Shutdown(destroyGameObject: true, shutdownReason: ShutdownReason.Ok);
            else if (toShutdown.gameObject != null) Destroy(toShutdown.gameObject);
        }
        catch (Exception) { if (toShutdown?.gameObject != null) Destroy(toShutdown.gameObject); }
        playerDataMap.Clear(); playerDataList.Clear();
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        StringBuilder sb = new StringBuilder(6);
        for (int i = 0; i < 6; i++) sb.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);
        return sb.ToString();
    }

    private int ResolveSelectedGameSceneBuildIndex()
    {
        for (int i = 0; i < mapSceneBindings.Length; i++)
        {
            if (mapSceneBindings[i].mapId == selectedMapId)
            {
                int idx = FindBuildIndexBySceneName(mapSceneBindings[i].sceneName);
                if (idx >= 0) return idx;
            }
        }
        return gameSceneBuildIndex;
    }

    private static int FindBuildIndexBySceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return -1;
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        string suffix = $"/{sceneName}.unity";
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    // ===================== INetworkRunnerCallbacks =====================

    public void OnPlayerJoined(NetworkRunner r, PlayerRef player)
    {
        if (!r.IsServer) return;
        if (playerDataList.Count >= defaultMaxPlayers) { r.Disconnect(player); return; }
        if (playerDataPrefab == null) return;

        // [상호 방어 코드] StateManager 쪽에 이미 장부가 등록되었거나 먼저 스폰되었다면 중복 생성을 즉시 차단합니다.
        if (FusionRoomStateManager.Instance != null &&
            (FusionRoomStateManager.Instance.GetPlayerData(player) != null || playerDataMap.ContainsKey(player)))
        {
            Debug.Log($"[FusionRoomManager] FusionRoomStateManager 혹은 맵에 이미 {player} 데이터가 존재하므로 스폰을 건너뜁니다.");
            return;
        }

        r.Spawn(playerDataPrefab, Vector3.zero, Quaternion.identity, inputAuthority: player);
    }

    public void OnPlayerLeft(NetworkRunner r, PlayerRef player)
    {
        if (!r.IsServer) return;
        if (playerDataMap.TryGetValue(player, out PlayerData data) && data != null && data.Object != null)
        {

            foreach (var carryable in Carryable.All)
            {
                if (carryable != null &&
                    carryable.IsCarried &&
                    carryable.Object.StateAuthority == player)
                {
                    carryable.Drop(carryable.transform);
                }
            }

            r.Despawn(data.Object);
        }
    }

    public void OnShutdown(NetworkRunner r, ShutdownReason shutdownReason)
    {
        PlayerMove.ClearPendingQueues();
        if (r == runner) runner = null;
        playerDataMap.Clear(); playerDataList.Clear(); CurrentRoomInfo = default;
        OnRoomShutdown?.Invoke();
    }

    public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessionList) { latestSessionList = sessionList; OnSessionListReceived?.Invoke(sessionList); }
    public void OnConnectFailed(NetworkRunner r, NetAddress remoteAddress, NetConnectFailedReason reason) { OnJoinFailed?.Invoke($"접속 실패: {reason}"); }

    public void OnSceneLoadStart(NetworkRunner r) 
    {
        OnGameStarting?.Invoke();
    }
    public void OnSceneLoadDone(NetworkRunner r)
    {
        if (r.IsServer)
        {
            var lobbyScene = SceneManager.GetActiveScene();
            if (lobbyScene.name == "RoomLobby") SceneManager.SetActiveScene(lobbyScene);
        }
    }

    #region Host Migration
    public async void OnHostMigration(NetworkRunner r, HostMigrationToken hostMigrationToken)
    {
        await EnsureCleanRunnerAsync();
        runner = CreateRunner(isLobbyOnly: false);
        await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,
            HostMigrationToken = hostMigrationToken,
            HostMigrationResume = HostMigrationResume,
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
        });
    }

    private void HostMigrationResume(NetworkRunner r)
    {
        foreach (var player in r.ActivePlayers)
        {
            if (!r.IsServer) continue;
            if (playerDataMap.ContainsKey(player)) continue;

            NetworkObject spawnedObj = r.Spawn(
                playerDataPrefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: player
            );

            // ★ 새 호스트의 IsRoomHost 갱신
            if (spawnedObj != null && player == r.LocalPlayer)
            {
                PlayerData data = spawnedObj.GetComponent<PlayerData>();
                if (data != null && data.Object.HasStateAuthority)
                    data.IsRoomHost = true;
            }
        }
    }
    #endregion

    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason)
    {
        UIEventManager.TriggerShowToast("호스트 연결이 끊어졌습니다..");

        // ★ Fusion이 OnShutdown을 자동 호출 → OnRoomShutdown → RoomShutdownHandler가 타이틀로 이동
        // 단, OnShutdown이 오지 않는 경우를 대비해 명시적으로 Shutdown 호출
        if (r != null && r.IsRunning)
            _ = r.Shutdown(destroyGameObject: true, shutdownReason: ShutdownReason.PhotonCloudTimeout);

    }

    public void OnConnectedToServer(NetworkRunner r) { }

    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnInput(NetworkRunner r, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
    public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef player, ReliableKey key, float progress) { }
    public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
}