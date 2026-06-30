using System;
using Fusion;
using UnityEngine;

public class AppQuickStarter : SimulationBehaviour, IPlayerJoined
{
    [SerializeField] private NetworkRunner _networkRunner;
    [SerializeField] private NetworkObject _networkObject;
    [SerializeField] private NetworkObject _playerPrefab;

    private bool enemySpawn = true;
    private void OnGUI()
    {
        // x, y, width, height
        if (GUI.Button(new Rect(10, 10, 120, 40), "게임 시작"))
        {
            StartGame();
        }
    }
    public async void StartGame()
    {
        var result = await _networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = "Test_Salvage1112"
        });
    }
    public void PlayerJoined(PlayerRef player)
    {

        if (_playerPrefab)
            _networkRunner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity, player);
    }

    public override void FixedUpdateNetwork()
    {
        if (enemySpawn)
        {
            enemySpawn = false;
            if (_networkObject)
                _networkRunner.Spawn(_networkObject, new Vector3(8, 0, 8), Quaternion.identity);
        }
    }
}
