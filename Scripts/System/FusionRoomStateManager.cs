// File: Assets/@Project/Scripts/System/FusionRoomStateManager.cs
using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/* * 기존 퓨전 룸 매니저의 GodManager를 스크립트 상태로 분리
*/

public class FusionRoomStateManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static FusionRoomStateManager Instance { get; private set; }

    // ★ 인스펙터 중복 등록 제거: playerDataPrefab 필드 삭제!
    // 대신 FusionRoomManager의 프리팹을 공유하여 사용합니다.

    private readonly Dictionary<PlayerRef, PlayerData> playerDataMap = new Dictionary<PlayerRef, PlayerData>();
    private readonly List<PlayerData> playerDataList = new List<PlayerData>();

    public IReadOnlyList<PlayerData> PlayerDataList => playerDataList;

    public event Action<PlayerData> OnPlayerDataSpawned;
    public event Action<PlayerData> OnPlayerDataDespawned;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterPlayerData(PlayerData data)
    {
        if (data == null) return;
        PlayerRef key = data.PlayerRef;
        if (!playerDataMap.ContainsKey(key))
        {
            playerDataMap[key] = data;
            if (!playerDataList.Contains(data))
            {
                playerDataList.Add(data);
            }
            OnPlayerDataSpawned?.Invoke(data);
            Debug.Log($"[StateManager] 플레이어 데이터 등록 성공: {key}");
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
            Debug.Log($"[StateManager] 플레이어 데이터 해제: {key}");
        }
    }

    public PlayerData GetPlayerData(PlayerRef player)
        => playerDataMap.TryGetValue(player, out PlayerData data) ? data : null;

    public bool IsColorTaken(int colorId, PlayerRef exceptPlayer)
    {
        if (colorId < 0) return false;
        for (int i = 0; i < playerDataList.Count; i++)
        {
            PlayerData data = playerDataList[i];
            if (data == null || data.PlayerRef == exceptPlayer) continue;
            if (data.ColorId == colorId) return true;
        }
        return false;
    }

    public void ClearData()
    {
        playerDataMap.Clear();
        playerDataList.Clear();
    }

    public void OnSceneLoadDone(NetworkRunner r)
    {
        if (r == null || !r.IsRunning)
        {
            Debug.LogError("[StateManager] 🚨 씬 로드는 끝났으나, 네트워크 연결이 이미 유실(Timeout)되었습니다. 장부 재매핑을 취소합니다.");
            return;
        }

        try
        {
            if (r.SimulationUnityScene == null)
            {
                Debug.LogWarning("[StateManager] SimulationUnityScene 참조가 유효하지 않습니다.");
                return;
            }

            Debug.Log($"[StateManager] 씬 로드 완료. 플레이어 데이터 장부 재매핑 시작.");

            playerDataMap.Clear();
            playerDataList.Clear();

            foreach (var simObj in r.SimulationUnityScene.GetComponents<NetworkObject>(true))
            {
                if (simObj == null) continue;

                if (simObj.TryGetComponent<PlayerData>(out var data))
                {
                    if (data.PlayerRef != PlayerRef.None)
                    {
                        playerDataMap[data.PlayerRef] = data;
                        playerDataList.Add(data);
                        Debug.Log($"[StateManager] 인게임 씬 데이터 연결 성공: {data.PlayerRef} (닉네임: {data.Nickname})");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[StateManager] 장부 재매핑 중 예외 발생 (네트워크 유실 의심): {e.Message}\n{e.StackTrace}");
        }
    }

    public void OnPlayerJoined(NetworkRunner r) { } // 퓨전 2 기본 인터페이스 맞춤용 오버로드

    public void OnPlayerJoined(NetworkRunner r, PlayerRef player)
    {
        if (!r.IsServer) return;

        // ★ [상호 방어 코드 1] FusionRoomManager 쪽에 이미 장부가 등록되었거나 스폰되었다면 중복 생성을 즉시 차단합니다.
        if (FusionRoomManager.Instance != null &&
            (FusionRoomManager.Instance.GetPlayerData(player) != null || FusionRoomManager.Instance.PlayerDataList.Count >= r.Config.Simulation.PlayerCount))
        {
            Debug.Log($"[StateManager] FusionRoomManager에서 이미 {player}를 스폰했거나 장부가 등록되어 스폰을 건너뜁니다.");
            return;
        }

        // ★ [상호 방어 코드 2] 내 장부 자체에도 이미 존재한다면 리턴합니다.
        if (playerDataMap.ContainsKey(player))
        {
            Debug.Log($"[StateManager] 이미 {player}의 데이터 객체가 존재하므로 중복 스폰을 취소합니다.");
            return;
        }

        // ★ 인스펙터 중복 할당 방지: FusionRoomManager의 프리팹 참조를 공유해서 사용
        NetworkObject targetPrefab = null;
        if (FusionRoomManager.Instance != null)
        {
            targetPrefab = FusionRoomManager.Instance.PlayerDataPrefab;
        }

        if (targetPrefab == null)
        {
            Debug.LogError("[StateManager] FusionRoomManager의 playerDataPrefab이 지정되지 않았거나 인스펙터에 할당되지 않았습니다.");
            return;
        }

        NetworkObject spawnedDataObj = r.Spawn(targetPrefab, Vector3.zero, Quaternion.identity, inputAuthority: player);

        if (spawnedDataObj != null)
        {
            GameObject.DontDestroyOnLoad(spawnedDataObj.gameObject);
            Debug.Log($"[StateManager] 호스트가 {player}의 데이터를 스폰하고 파괴 면제 구역으로 이동시켰습니다.");
        }
    }

    public void OnPlayerLeft(NetworkRunner r, PlayerRef player)
    {
        if (!r.IsServer) return;

        // 내 장부에 있으면 내가 지우고, 없으면 RoomManager가 지우도록 상호 체크 구조 유지
        if (playerDataMap.TryGetValue(player, out PlayerData data) && data != null && data.Object != null)
        {
            r.Despawn(data.Object);
        }
    }

    public void OnShutdown(NetworkRunner r, ShutdownReason shutdownReason) => ClearData();

    public void OnConnectedToServer(NetworkRunner r) { }
    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner r, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnInput(NetworkRunner r, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
    public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    public void OnSceneLoadStart(NetworkRunner r) { }
    public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessionList) { }
    public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
    public void OnHostMigration(NetworkRunner r, HostMigrationToken hostMigrationToken) { }
}