// File: Assets/@Project/Scripts/System/FusionNetworkBootstrapper.cs
using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

public class FusionNetworkBootstrapper : MonoBehaviour
{
    public static FusionNetworkBootstrapper Instance { get; private set; }
    public NetworkRunner Runner { get; private set; }

    private void Awake()
    {
        Application.runInBackground = true;
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public NetworkRunner CreateCleanRunner(bool isLobbyOnly)
    {
        GameObject go = new GameObject(isLobbyOnly ? "LobbyRunner" : "NetworkRunner");
        DontDestroyOnLoad(go);

        // 프레임 초기화 순서 리스크 방지를 위해 SceneManager를 Runner보다 먼저 생성
        go.AddComponent<NetworkSceneManagerDefault>();

        Runner = go.AddComponent<NetworkRunner>();
        Runner.ProvideInput = !isLobbyOnly;

        return Runner;
    }


    public async Task DisposeActiveRunnerAsync()
    {
        if (Runner == null) return;

        NetworkRunner toDispose = Runner;
        Runner = null; // 대기 시작 전 참조를 끊어 후속 진입 요청과의 충돌 방지

        try
        {
            if (toDispose.IsRunning || toDispose.State != NetworkRunner.States.Shutdown)
            {
                await toDispose.Shutdown(destroyGameObject: true, shutdownReason: ShutdownReason.Ok);
            }
            else if (toDispose.gameObject != null)
            {
                Destroy(toDispose.gameObject);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Bootstrapper] Runner dispose failed: {e.Message}");
            if (toDispose != null && toDispose.gameObject != null) Destroy(toDispose.gameObject);
        }
    }
}
