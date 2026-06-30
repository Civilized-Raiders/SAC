using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class QStarter : SimulationBehaviour, IPlayerJoined
{
    [SerializeField] private NetworkRunner _networkRunner;
    [SerializeField] private NetworkObject _networkObject;
    [SerializeField] private string _sessionName = "Test_Salvage";
    
    public async void Start()
    {
        var sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null) sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        // [핵심 추가] NetworkRigidbody3D가 동작하려면 Runner에 이 컴포넌트가 반드시 있어야 함.
        // StartGame 전에 추가해야 spawn 타이밍에 경고/누락이 없음.
        if (_networkRunner.GetComponent<RunnerSimulatePhysics3D>() == null)
            _networkRunner.gameObject.AddComponent<RunnerSimulatePhysics3D>();

        var sceneInfo = new NetworkSceneInfo();
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(activeScene.buildIndex), UnityEngine.SceneManagement.LoadSceneMode.Single);

        var result = await _networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = _sessionName,
            SceneManager = sceneManager,
            Scene = sceneInfo
        });

        Debug.Log($"Start Result : {result.Ok}");

    }

    public void PlayerJoined(PlayerRef player)
    {
        Debug.Log($"PlayerJoined : {player}");

        if (!_networkRunner.IsServer) return;
        _networkRunner.Spawn(_networkObject, Vector3.zero, Quaternion.identity, player);

    }
}