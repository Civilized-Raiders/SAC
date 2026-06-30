/* File: Assets/@Project/Scripts/System/PlayerSpawner.cs
* Role : 게임플레이 씬에서 실제 플레이어(탱크) 프리팹을 스폰하는 책임을 진다.
*        로비에서 RequestStartGame() -> runner.LoadScene(gameScene) 로 같은 Runner가
*        게임 씬으로 넘어오면, 이 스포너가 Host에서 각 PlayerRef 의 inputAuthority 로
*        Player.prefab 을 스폰한다.
*
*        ★ 색 적용은 Player.prefab 에 붙어 있는 PlayerColor 가 전부 처리한다.
*          - PlayerColor.Spawned() 가 Host 에서
*            FusionRoomManager.GetPlayerData(Object.InputAuthority).ColorId 를 읽어
*            Networked ColorId 에 박고, 모든 클라가 ApplyColor() 로 색을 칠한다.
*          - 따라서 이 스포너의 유일한 책임은 "inputAuthority 를 정확히 넘겨" 스폰하는 것.
*            inputAuthority 가 어긋나면 PlayerColor 가 엉뚱한 PlayerData 의 색을 읽게 된다.
*
*        QStarter 를 대체한다. 게임 씬에 QStarter 가 남아 있으면 둘 다 스폰돼
*        중복 플레이어가 생기므로, 게임 씬에는 QStarter 대신 이 컴포넌트를 둔다.
*/

using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerSpawner : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("Spawn")]
    [Tooltip("PlayerColor / PlayerMove 가 붙은 게임플레이용 Player 프리팹 (Player.prefab)")]
    [SerializeField] private NetworkObject playerPrefab;

    [Tooltip("스폰 위치들. 비어 있으면 원점에 스폰. PlayerId 순서대로 배정한다.")]
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();

    public override void Spawned()
    {
        if (!Object.HasStateAuthority) return;

        // 콜백  등록
        Runner.AddCallbacks(this);

        var playerList = Runner.ActivePlayers.ToList();
        for (int i = 0; i < playerList.Count; i++)
        {
            var obj = Runner.Spawn(playerPrefab, spawnPoints[i].position, Quaternion.identity, playerList[i]);
            if (obj != null)
                spawnedPlayers[playerList[i]] = obj;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        runner?.RemoveCallbacks(this); // 러너 등록 해제
        spawnedPlayers.Clear();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        if (spawnedPlayers.TryGetValue(player, out var obj))
        {
            if (obj != null) Runner.Despawn(obj);
            spawnedPlayers.Remove(player);
            Debug.Log($"[PlayerSpawner] {player} 이탈 — 디스폰 완료");
        }
    }

    // 나머지 필수 빈 구현
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { UIEventManager.TriggerShowToast("연결에 실패하셨습니다."); }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}
