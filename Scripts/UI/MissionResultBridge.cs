using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissionResultBridge : MonoBehaviour
{
    [SerializeField] private MissionResultPopupUI resultPopup;
    [SerializeField] private MissionResultVideoUI resultVideoUI;
    [SerializeField] private float hostLeaveDelaySeconds = 1.5f;

    private NetworkGameManager networkGameManager;
    private FusionRoomManager fusionRoomManager;
    private bool resultShown;
    private Coroutine leaveRoomRoutine;

    private void Awake()
    {
        if (resultPopup == null)
        {
            resultPopup = GetComponent<MissionResultPopupUI>();
        }

        if (resultPopup == null)
        {
            resultPopup = GetComponentInChildren<MissionResultPopupUI>(true);
        }

        if (resultPopup == null)
        {
            resultPopup = FindInactivePopupInLoadedScene();
        }

        if (resultVideoUI == null)
        {
            resultVideoUI = GetComponent<MissionResultVideoUI>();
        }

        if (resultVideoUI == null)
        {
            resultVideoUI = GetComponentInChildren<MissionResultVideoUI>(true);
        }

        if (resultVideoUI == null)
        {
            resultVideoUI = FindInactiveVideoUIInLoadedScene();
        }
    }

    private void OnEnable()
    {
        ResolveManagers();
        BindResultEvents();
        SubscribeNetworkGameManager();
    }

    private void OnDisable()
    {
        if (resultPopup != null)
        {
            resultPopup.ConfirmRequested -= HandleConfirmRequested;
        }

        if (resultVideoUI != null)
        {
            resultVideoUI.ConfirmRequested -= HandleConfirmRequested;
        }

        UnsubscribeNetworkGameManager();

        if (leaveRoomRoutine != null)
        {
            StopCoroutine(leaveRoomRoutine);
            leaveRoomRoutine = null;
        }
    }

    private void Update()
    {
        if (resultShown)
        {
            GameplayInputGuard.ApplyCurrentCursorState();
        }

        if (networkGameManager == null || fusionRoomManager == null)
        {
            ResolveManagers();
            SubscribeNetworkGameManager();
        }

        if (resultPopup == null)
        {
            resultPopup = FindInactivePopupInLoadedScene();
            BindResultEvents();
        }

        if (resultVideoUI == null)
        {
            resultVideoUI = FindInactiveVideoUIInLoadedScene();
            BindResultEvents();
        }

        if (!resultShown && IsNetworkGameManagerUsable())
        {
            if (networkGameManager.IsGameEnded)
            {
                StartCoroutine(ShowResultWhenResolved());
                return;
            }

            if (networkGameManager.RemainingTimeSeconds <= 0)
            {
                ShowResult(false);
            }
        }

        // 대기 중이고 게임이 끝나면 나가기
        if (_waitingForGameEnd && IsNetworkGameManagerUsable() && networkGameManager.IsGameEnded)
        {
            _waitingForGameEnd = false;
            StartCoroutine(LeaveAfterDelay()); 
        }
    }

    private void ResolveManagers()
    {
        if (networkGameManager == null)
        {
            networkGameManager = NetworkGameManager.Instance;
            if (networkGameManager == null)
            {
                networkGameManager = FindFirstObjectByType<NetworkGameManager>();
            }
        }

        if (fusionRoomManager == null)
        {
            fusionRoomManager = FusionRoomManager.Instance;
            if (fusionRoomManager == null)
            {
                fusionRoomManager = FindFirstObjectByType<FusionRoomManager>();
            }
        }
    }

    private void SubscribeNetworkGameManager()
    {
        if (!IsNetworkGameManagerUsable())
        {
            return;
        }

        networkGameManager.OnGameEnded -= HandleGameEnded;
        networkGameManager.OnGameEnded += HandleGameEnded;
    }

    private void UnsubscribeNetworkGameManager()
    {
        if (networkGameManager != null)
        {
            networkGameManager.OnGameEnded -= HandleGameEnded;
        }
    }

    private void HandleGameEnded(bool isSuccess)
    {
        // 즉시 판정하지 않고 HasEscaped replicate 대기
        StartCoroutine(ShowResultWhenResolved());
    }

    private IEnumerator ShowResultWhenResolved()
    {
        // 로컬 플레이어의 PlayerDeathHandler를 찾는다
        PlayerRef localPlayer = fusionRoomManager?.Runner?.LocalPlayer ?? default;
        PlayerDeathHandler localHandler = null;

        // 최대 1초 대기하며 handler 탐색
        float timeout = Time.realtimeSinceStartup + 3f;
        while (localHandler == null && Time.realtimeSinceStartup < timeout)
        {
            foreach (var h in FindObjectsByType<PlayerDeathHandler>(FindObjectsSortMode.None))
            {
                if (h.Object != null && h.Object.IsValid && h.Object.InputAuthority == localPlayer)
                {
                    localHandler = h;
                    break;
                }
            }
            yield return null;
        }

        if (localHandler == null)
        {
            ShowResult(false);
            yield break;
        }

        // IsDead 또는 HasEscaped 중 하나가 확정될 때까지 대기
        timeout = Time.realtimeSinceStartup + 3f;
        while (!localHandler.IsDead && !localHandler.HasEscaped
               && Time.realtimeSinceStartup < timeout)
        {
            yield return null;
        }

        ShowResult(localHandler.HasEscaped && !localHandler.IsDead);

    }


    private void ShowResult(bool isSuccess)
    {
        if (resultShown || !IsNetworkGameManagerUsable())
        {
            return;
        }

        bool canShowVideoResult = resultVideoUI != null && resultVideoUI.HasPlayableResult;
        if (!canShowVideoResult && resultPopup == null)
        {
            return;
        }

        resultShown = true;
        GameplayInputGuard.SetBlocked(true);

        if (canShowVideoResult)
        {
            if (resultPopup != null)
            {
                resultPopup.ConfirmRequested -= HandleConfirmRequested;
                resultPopup.enabled = false;
            }

            if (resultVideoUI.gameObject != null)
            {
                resultVideoUI.gameObject.SetActive(true);
            }

            resultVideoUI.ShowResult(isSuccess);
            return;
        }

        if (resultPopup.gameObject != null)
        {
            resultPopup.gameObject.SetActive(true);
        }

        resultPopup.enabled = true;
        resultPopup.ConfirmRequested -= HandleConfirmRequested;
        resultPopup.ConfirmRequested += HandleConfirmRequested;

        MissionResultPopupUI.PlayerResultViewData[] playerResults = BuildPlayerResults();
        int targetMoney = Mathf.Max(0, networkGameManager.targetMoney);
        int collectedMoney = Mathf.Max(0, networkGameManager.CurrentMoney);

        if (isSuccess)
        {
            resultPopup.ShowSuccess(targetMoney, collectedMoney, playerResults);
        }
        else
        {
            resultPopup.ShowFail(targetMoney, collectedMoney, playerResults);
        }
    }

    private MissionResultPopupUI.PlayerResultViewData[] BuildPlayerResults()
    {
        List<MissionResultPopupUI.PlayerResultViewData> results = new List<MissionResultPopupUI.PlayerResultViewData>();
        Dictionary<PlayerRef, PlayerDeathHandler> deathHandlers = BuildDeathHandlerMap();

        IReadOnlyList<PlayerData> playerDataList = fusionRoomManager != null ? fusionRoomManager.PlayerDataList : null;
        if (playerDataList != null && playerDataList.Count > 0)
        {
            for (int i = 0; i < playerDataList.Count; i++)
            {
                PlayerData data = playerDataList[i];
                if (data == null)
                {
                    continue;
                }

                deathHandlers.TryGetValue(data.PlayerRef, out PlayerDeathHandler deathHandler);
                results.Add(BuildPlayerResult(data, deathHandler));
            }
        }
        else
        {
            foreach (KeyValuePair<PlayerRef, PlayerDeathHandler> pair in deathHandlers)
            {
                results.Add(BuildPlayerResult(null, pair.Value));
            }
        }

        return results.ToArray();
    }

    private Dictionary<PlayerRef, PlayerDeathHandler> BuildDeathHandlerMap()
    {
        Dictionary<PlayerRef, PlayerDeathHandler> handlers = new Dictionary<PlayerRef, PlayerDeathHandler>();
        PlayerDeathHandler[] foundHandlers = FindObjectsByType<PlayerDeathHandler>(FindObjectsSortMode.None);
        for (int i = 0; i < foundHandlers.Length; i++)
        {
            PlayerDeathHandler handler = foundHandlers[i];
            if (handler == null || handler.Object == null || !handler.Object.IsValid)
            {
                continue;
            }

            handlers[handler.Object.InputAuthority] = handler;
        }

        return handlers;
    }

    private MissionResultPopupUI.PlayerResultViewData BuildPlayerResult(PlayerData data, PlayerDeathHandler deathHandler)
    {
        bool hasPlayer = data != null || deathHandler != null;
        bool isEscapedAlive = deathHandler != null && deathHandler.HasEscaped && !deathHandler.IsDead;
        int resolvedSeconds = deathHandler != null && deathHandler.ResolvedAtSeconds >= 0
            ? deathHandler.ResolvedAtSeconds
            : (networkGameManager != null ? networkGameManager.ElapsedTimeSeconds : 0);

        return new MissionResultPopupUI.PlayerResultViewData
        {
            hasPlayer = hasPlayer,
            playerName = data != null ? data.Nickname.ToString() : "Player",
            portrait = null,
            isAlive = isEscapedAlive,
            survivalTimeText = FormatSeconds(resolvedSeconds),
            collectedItemCount = 0,
            specialArtifactCount = 0
        };
    }

    
    private bool _waitingForGameEnd = false;

    private void BindResultEvents()
    {
        if (resultPopup != null)
        {
            resultPopup.ConfirmRequested -= HandleConfirmRequested;
        }

        if (resultVideoUI != null)
        {
            resultVideoUI.ConfirmRequested -= HandleConfirmRequested;
            resultVideoUI.ConfirmRequested += HandleConfirmRequested;
        }

        if (resultPopup != null && !ShouldUseVideoResult())
        {
            resultPopup.ConfirmRequested += HandleConfirmRequested;
        }
    }

    private bool ShouldUseVideoResult()
    {
        return resultVideoUI != null && resultVideoUI.HasPlayableResult;
    }

    private void HandleConfirmRequested(bool isSuccess)
    {
        if (fusionRoomManager == null) return;

        GameplayVideoAudioMuteController.ForceRestoreGameAudio();

        if (!IsNetworkGameManagerUsable() || networkGameManager.IsGameEnded)
        {
            StartLeaveRoomAfterResultVideo();
            return;
        }

        _waitingForGameEnd = true;
        //NET : failCountdownText, successCountdownText같은걸로 대체 가능합니다.
        UIEventManager.TriggerShowToast("다른 플레이어를 기다리는 중...");
    }

    private void StartLeaveRoomAfterResultVideo()
    {
        if (leaveRoomRoutine != null)
        {
            return;
        }

        leaveRoomRoutine = StartCoroutine(LeaveRoomAfterResultVideoRoutine());
    }

    private IEnumerator LeaveRoomAfterResultVideoRoutine()
    {
        NetworkRunner runner = fusionRoomManager != null ? fusionRoomManager.Runner : null;
        bool isHost = runner != null && runner.IsServer;
        bool hasOtherPlayers = GetActivePlayerCount(runner) > 1;
        float delaySeconds = isHost && hasOtherPlayers ? Mathf.Max(0f, hostLeaveDelaySeconds) : 0f;
        bool mutedDuringDelay = delaySeconds > 0f;

        if (mutedDuringDelay)
        {
            GameplayVideoAudioMuteController.MuteGameAudio();
        }

        float endAt = Time.realtimeSinceStartup + delaySeconds;
        while (Time.realtimeSinceStartup < endAt)
        {
            GameplayInputGuard.ApplyCurrentCursorState();
            if (mutedDuringDelay)
            {
                GameplayVideoAudioMuteController.RefreshGameAudioMute();
            }

            yield return null;
        }

        if (mutedDuringDelay)
        {
            GameplayVideoAudioMuteController.RestoreGameAudio();
        }

        GameplayVideoAudioMuteController.ForceRestoreGameAudio();

        if (fusionRoomManager != null)
        {
            fusionRoomManager.LeaveRoom();
        }

        leaveRoomRoutine = null;
    }

    private static int GetActivePlayerCount(NetworkRunner runner)
    {
        if (runner == null)
        {
            return 0;
        }

        int count = 0;
        foreach (PlayerRef _ in runner.ActivePlayers)
        {
            count++;
        }

        return count;
    }

    private IEnumerator LeaveAfterDelay() 
    {
        int countdown = 5; //이거는 임의의 숫자로 늘려도 되기는하나 5-10초 사이의 숫자로 변경하는걸 추천드립니다.
        while (countdown > 0)
        {
            //NET : failCountdownText, successCountdownText같은걸로 대체 가능합니다.
            UIEventManager.TriggerShowToast($"{countdown}초 후 타이틀로 이동합니다.");
            yield return new WaitForSeconds(1f);
            countdown--;
        }

        resultShown = false;  // Update()의 커서 잠금 루프 차단
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameplayVideoAudioMuteController.ForceRestoreGameAudio();

        fusionRoomManager.LeaveRoom();
    }



    private bool IsNetworkGameManagerUsable()
    {
        return networkGameManager != null && networkGameManager.Object != null && networkGameManager.Object.IsValid;
    }

    private static MissionResultPopupUI FindInactivePopupInLoadedScene()
    {
        MissionResultPopupUI[] popups = Resources.FindObjectsOfTypeAll<MissionResultPopupUI>();
        for (int i = 0; i < popups.Length; i++)
        {
            MissionResultPopupUI popup = popups[i];
            if (popup == null)
            {
                continue;
            }

            if (popup.gameObject.scene.IsValid() && popup.gameObject.scene.isLoaded)
            {
                return popup;
            }
        }

        return null;
    }

    private static MissionResultVideoUI FindInactiveVideoUIInLoadedScene()
    {
        MissionResultVideoUI[] videoUIs = Resources.FindObjectsOfTypeAll<MissionResultVideoUI>();
        for (int i = 0; i < videoUIs.Length; i++)
        {
            MissionResultVideoUI videoUI = videoUIs[i];
            if (videoUI == null)
            {
                continue;
            }

            if (videoUI.gameObject.scene.IsValid() && videoUI.gameObject.scene.isLoaded)
            {
                return videoUI;
            }
        }

        return null;
    }

    private static string FormatSeconds(int totalSeconds)
    {
        int safeSeconds = Mathf.Max(0, totalSeconds);
        int minutes = safeSeconds / 60;
        int seconds = safeSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}
