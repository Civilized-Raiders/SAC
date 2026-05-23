using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fusion 연결 단일 진입점.
/// W1 (5/23): 공개방/비밀방 생성·참가, 나가기, 플레이어 목록 이벤트 알림.
/// M1 (5/24): Ready 동기화, Host StartGame → 게임씬 로드, Player Spawn으로 확장.
/// </summary>
public class FusionRoomManager : MonoBehaviour, INetworkRunnerCallbacks
{
    // ─────────────────────────────────── 상수

    // 공개방 고정 세션명.
    // Host가 생성하고 Client가 이 이름으로 직접 접속한다.
    private const string PublicRoomSession = "JunkRaiders_Public";

    // ─────────────────────────────────── 설정 (Inspector 노출)

    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string roomLobbySceneName = "RoomLobby";

    #region 싱글턴

    static FusionRoomManager instance;

    public static FusionRoomManager Instance
    {
        get
        {
            if (instance == null)
                EnsureInstance();

            return instance;
        }
    }

    #endregion

    // ─────────────────────────────────── 내부 상태

    NetworkRunner runner;
    bool isStarting;
    bool isShuttingDown;
    bool isQuitting;

    // LeaveRoom 완료 후 호출할 콜백
    Action pendingShutdownCallback;



    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    static void EnsureInstance()
    {
        FusionRoomManager existing = FindFirstObjectByType<FusionRoomManager>();

        if (existing != null)
        {
            instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return;
        }

        GameObject go = new GameObject(nameof(FusionRoomManager));

        instance = go.AddComponent<FusionRoomManager>();

        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DontDestroyOnLoad(gameObject);


        // 에디터/빌드 모두에서 포커스 이탈 시 연결 끊김 방지
        Application.runInBackground = true;

    }

    #region 공개 프로퍼티

    public bool IsHost => runner != null && runner.IsServer;

    public NetworkRunner Runner => runner;

    public string CurrentSessionName => runner?.SessionInfo?.Name ?? string.Empty;

    #endregion

    public IEnumerable<PlayerRef> GetActivePlayers()
    {
        if (runner == null)
            return Array.Empty<PlayerRef>();

        return runner.ActivePlayers;
    }

    #region 이벤트 (RoomPanel 등 UI가 구독)

    /// <summary>
    /// 플레이어가 방에 입장했을 때
    /// (RoomPanel 플레이어 목록 갱신용)
    /// </summary>
    public event Action<PlayerRef, NetworkRunner> OnPlayerJoinedCallback;

    /// <summary>
    /// 플레이어가 방을 나갔을 때
    /// (RoomPanel 플레이어 목록 갱신용)
    /// </summary>
    public event Action<PlayerRef, NetworkRunner> OnPlayerLeftCallback;

    /// <summary>
    /// Runner가 종료됐을 때
    /// (외부 강제 종료, 네트워크 오류 등)
    /// </summary>
    public event Action OnRunnerShutdownCallback;

    #endregion

    // ═════════════════════════════════════════════════════════════
    // 방 생성 / 참가
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// 공개방 생성
    /// (Host Mode, 고정 세션명 PublicRoomSession)
    /// </summary>
    public void CreatePublicRoom(Action onSuccess, Action<string> onFailed)
    {
        if (isStarting || isShuttingDown)
        {
            onFailed?.Invoke("Already starting or shutting down a session.");
            return;
        }

        _ = StartSessionAsync(GameMode.Host, PublicRoomSession, onSuccess, onFailed);
    }

    /// <summary>
    /// 비밀방 생성
    /// (Host Mode, roomCode를 세션명으로 사용)
    /// </summary>
    public void CreatePrivateRoom(string roomCode, Action onSuccess, Action<string> onFailed)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            onFailed?.Invoke("Room code is empty.");
            return;
        }

        if (isStarting || isShuttingDown)
        {
            onFailed?.Invoke(
                "Already starting or shutting down a session.");

            return;
        }

        _ = StartSessionAsync(GameMode.Host, roomCode, onSuccess, onFailed);
    }

    /// <summary>
    /// 빠른 참가(공개방에 AutoHostOrClient로 접속)
    /// </summary>
    public void JoinQuickRoom(Action onSuccess, Action<string> onFailed)
    {
        if (isStarting || isShuttingDown)
        {
            onFailed?.Invoke("Already starting or shutting down a session.");
            return;
        }

        _ = StartSessionAsync(GameMode.AutoHostOrClient, PublicRoomSession, onSuccess, onFailed);
    }

    /// <summary>
    /// 코드로 비밀방 참가(Client Mode, roomCode = 세션명)
    /// </summary>
    public void JoinRoomByCode(string roomCode, Action onSuccess, Action<string> onFailed)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            onFailed?.Invoke("Room code is empty.");
            return;
        }

        if (isStarting || isShuttingDown)
        {
            onFailed?.Invoke("Already starting or shutting down a session.");
            return;
        }

        _ = StartSessionAsync(GameMode.Client, roomCode, onSuccess, onFailed);
    }

    // ═════════════════════════════════════════════════════════════
    // 나가기
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// 방 나가기.
    /// Shutdown 완료 후 onComplete 호출.
    /// </summary>
    public void LeaveRoom(Action onComplete = null)
    {
        _ = LeaveRoomAsync(onComplete);
    }

    private async Task LeaveRoomAsync(Action onComplete)
    {
        if (isShuttingDown) return;

        isShuttingDown = true;

        try
        {
            if (runner == null)
            {
                isStarting = false;
                isShuttingDown = false;

                onComplete?.Invoke();
                return;
            }

            // OnShutdown에서 실행할 콜백 저장
            pendingShutdownCallback = onComplete;

            await runner.Shutdown();
        }
        catch (Exception e)
        {
            Debug.LogError($"[FusionRoomManager] LeaveRoomAsync Exception: {e}");

            isShuttingDown = false;

            onComplete?.Invoke();
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 게임 흐름 (M1 5/24 이후 확장)
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// 준비 상태 토글.TODO M1 5/24: [Networked] bool 또는 RPC 동기화 구현 예정.
    /// </summary>
    public void Ready()
    {
        if (runner == null) return;

        Debug.Log("[FusionRoomManager] TODO M1 5/24: Ready sync.");
    }

    /// <summary>
    /// 게임 시작. Host만 호출 가능.
    /// </summary>
    public void StartGame()
    {
        if (runner == null || !runner.IsServer)
        {
            Debug.LogWarning("[FusionRoomManager] StartGame은 Host만 가능합니다.");

            return;
        }

        Debug.Log("[FusionRoomManager] TODO M1 5/24: StartGame.");
    }





    // ═════════════════════════════════════════════════════════════
    // 내부 공통 세션 시작
    // ═════════════════════════════════════════════════════════════

    private async Task StartSessionAsync(GameMode mode, string sessionName, Action onSuccess, Action<string> onFailed)
    {
        if (isStarting || isShuttingDown)
            return;

        isStarting = true;

        try
        {
            // 기존 Runner 정리
            if (runner != null)
            {
                isShuttingDown = true;

                await runner.Shutdown();

                CleanupRunner();

                isShuttingDown = false;
            }

            // Runner 전용 오브젝트 생성
            GameObject runnerObject = new GameObject("NetworkRunner");

            DontDestroyOnLoad(runnerObject);

            runner = runnerObject.AddComponent<NetworkRunner>();

            runner.ProvideInput = true;

            runner.AddCallbacks(this);

            // SceneManager = null: 씬 전환은 각 클라이언트가 SceneManager.LoadScene으로 직접 처리.
            // NetworkSceneManagerDefault를 쓰면 Fusion이 씬 동기화를 시도하지만,
            // 이 프로젝트는 아직 RoomLobby에 NetworkObject가 없으므로 불필요하고
            // 수동 LoadScene과 혼용하면 DisconnectByServerTimeout을 유발한다.
            StartGameArgs args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                PlayerCount = maxPlayers,
            };

            StartGameResult result = await runner.StartGame(args);

            // 수정 후
            if (result.Ok)
            {
                Debug.Log($"[FusionRoomManager] Session OK. Mode:{mode} Session:{sessionName}");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[FusionRoomManager] StartGame failed: {result.ShutdownReason}");

                // ServerInRoom: 이전 Host 세션이 Photon 클라우드에서 아직 정리되지 않은 상태.
                // (runner.Shutdown() 직후 같은 세션명으로 재접속 시 발생 — 클라우드 정리 지연 최대 ~10s)
                // Host 재시도 대신 AutoHostOrClient로 폴백.
                // → 세션이 살아있으면 Client로 합류, 없으면 Host로 생성.
                if (result.ShutdownReason == ShutdownReason.ServerInRoom
                    && mode == GameMode.Host)
                {
                    Debug.LogWarning($"[FusionRoomManager] ServerInRoom fallback: retrying as AutoHostOrClient for '{sessionName}'");
                    CleanupRunner();
                    isStarting = false;
                    _ = StartSessionAsync(GameMode.AutoHostOrClient, sessionName, onSuccess, onFailed);
                    return;
                }

                CleanupRunner();
                onFailed?.Invoke(result.ShutdownReason.ToString());
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FusionRoomManager] StartSessionAsync exception: {e}");

            CleanupRunner();

            onFailed?.Invoke(e.Message);
        }
        finally
        {
            isStarting = false;
        }
    }

    // ═════════════════════════════════════════════════════════════
    // Runner 정리
    // ═════════════════════════════════════════════════════════════

    private void CleanupRunner()
    {
        if (runner == null) return;

        if (runner.gameObject != null)
        {
            Destroy(runner.gameObject);
        }

        runner = null;
    }

    // ═════════════════════════════════════════════════════════════
    // INetworkRunnerCallbacks
    // ═════════════════════════════════════════════════════════════

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log(
            $"[FusionRoomManager] OnPlayerJoined: {player}");

        OnPlayerJoinedCallback?.Invoke(player, runner);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log(
            $"[FusionRoomManager] OnPlayerLeft: {player}");

        OnPlayerLeftCallback?.Invoke(player, runner);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[FusionRoomManager] OnShutdown: {shutdownReason}");

        CleanupRunner();

        isStarting = false;
        isShuttingDown = false;

        OnRunnerShutdownCallback?.Invoke();

        Action callback = pendingShutdownCallback;

        pendingShutdownCallback = null;

        if (!isQuitting)
        {
            callback?.Invoke();
        }
    }

    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        if (isQuitting) return;
        Debug.Log($"[FusionRoomManager] " + $"OnDisconnectedFromServer: {reason}");



        CleanupRunner();

        LobbySession.Clear();

        if (SceneManager.GetActiveScene().name != titleSceneName)
        {
            SceneManager.LoadScene(titleSceneName);
        }
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"[FusionRoomManager] OnConnectFailed: {reason}");
    }

    #region 미사용 콜백 스텁 (M1 5/24 이후 필요 시 구현)

    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    #endregion
}