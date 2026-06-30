using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-05-26 : PlayerInputProvider 무한궤도(탱크) 방식 입력으로 수정
 *              - Vertical(W/S)을 Z축 이동으로 할당
 *              - Horizontal(A/D)을 X축 조향으로 할당
 * ========================================================= */

public class PlayerInputProvider : MonoBehaviour, INetworkRunnerCallbacks
{
    private NetworkInputData inputdata;
    private bool resetButtons;
    private bool inputBlocked;


    private IEnumerator Start()
    {
        yield return null;

        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();

        if (runner != null)
        {
            runner.AddCallbacks(this);
            Debug.Log("[InputProvider] Callback Registered");
        }
    }

    private void Update()
    {
        if (inputBlocked || GameplayInputGuard.IsBlocked)
        {
            ClearInputData();
            return;
        }

        Vector3 move = Vector3.zero;

        // 💡 무한궤도(탱크/자동차)의 조향(A/D)은 X, 전후진(W/S)은 Y가 아니라 Z로 전달해야 합니다.
        move.x = Input.GetAxisRaw("Horizontal");
        move.z = Input.GetAxisRaw("Vertical");

        // 탱크 방식이므로 .normalized를 하면 대각선 조작(전진하며 회전) 시 
        // 전진 속도가 느려질 수 있어 정규화 없이 그대로 넘깁니다 (PlayerMove에서 개별 적용함).
        inputdata.moveDirection = move;

        inputdata.lookDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        if (resetButtons)
        {
            inputdata.buttons.Set(InputButton.Interact, false);
            inputdata.buttons.Set(InputButton.Carry, false);
            inputdata.buttons.Set(InputButton.Detach, false);
            inputdata.buttons.Set(InputButton.HeadLight, false);
            resetButtons = false;
        }

        // Sprint(달리기)
        inputdata.buttons.Set(InputButton.Sprint, Input.GetKey(KeyCode.LeftShift));

        // 상호작용 (E키)
        if (Input.GetKeyDown(KeyCode.E))
        {
            inputdata.buttons.Set(InputButton.Interact, true);
        }
        //버리기Q
        if (Input.GetKeyDown(KeyCode.Q))  // 원하는 키로 변경
        {
            inputdata.buttons.Set(InputButton.Detach, true);
        }
        if (Input.GetKeyDown(KeyCode.V))
        {
            inputdata.buttons.Set(InputButton.HeadLight, true);
        }


        if (Input.GetMouseButtonDown(0)) inputdata.buttons.Set(InputButton.Carry, true);

    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (inputBlocked || GameplayInputGuard.IsBlocked)
        {
            ClearInputData();
            input.Set(inputdata);
            resetButtons = true;
            return;
        }

        var data = inputdata;

        data.moveDirection = inputdata.moveDirection;
        data.lookDelta = inputdata.lookDelta;

        data.buttons.Set(InputButton.Sprint, Input.GetKey(KeyCode.LeftShift));

        input.Set(data);

        resetButtons = true;
    }

    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        if (inputBlocked)
        {
            ClearInputData();
        }
    }

    private void ClearInputData()
    {
        inputdata.moveDirection = Vector3.zero;
        inputdata.lookDelta = Vector2.zero;
        inputdata.buttons.Set(InputButton.Sprint, false);
        inputdata.buttons.Set(InputButton.Interact, false);
        inputdata.buttons.Set(InputButton.Carry, false);
        inputdata.buttons.Set(InputButton.Detach, false);
        inputdata.buttons.Set(InputButton.HeadLight, false);
        resetButtons = false;
    }



    public void OnConnectedToServer(NetworkRunner runner) { Debug.Log("[InputProvider] Connected To Server"); }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}
