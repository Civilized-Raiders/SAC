using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

public class MissionResultVideoUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject missionSuccessPanel;
    [SerializeField] private GameObject missionFailPanel;

    [Header("Videos")]
    [SerializeField] private VideoPlayer successVideoPlayer;
    [SerializeField] private VideoPlayer failVideoPlayer;
    [SerializeField] private float successFallbackSeconds = 8f;
    [SerializeField] private float failFallbackSeconds = 8f;
    [SerializeField] private bool confirmWhenVideoEnds;
    [SerializeField] private bool allowInputSkip;

    [Header("Legacy Result UI To Hide")]
    [SerializeField] private GameObject[] successObjectsToHide;
    [SerializeField] private GameObject[] failObjectsToHide;

    public event Action<bool> ConfirmRequested;

    private const float PrepareTimeoutSeconds = 10f;
    private const float PlaybackStartTimeoutSeconds = 5f;

    public bool HasPlayableResult =>
        successVideoPlayer != null ||
        failVideoPlayer != null ||
        missionSuccessPanel != null ||
        missionFailPanel != null;

    private Coroutine fallbackRoutine;
    private Coroutine playRoutine;
    private VideoPlayer currentVideoPlayer;
    private bool isShowing;
    private bool isShowingSuccess;
    private bool confirmRequested;
    private bool audioMuted;
    private bool currentVideoHasError;
    private Selectable[] disabledSelectables;
    private bool[] disabledSelectableStates;

    private void Awake()
    {
        AutoBindPanels();
        Hide();
    }

    private void OnDisable()
    {
        StopCurrentVideo();
        StopFallback();
        RestoreGameAudio();
        RestoreResultButtons();
    }

    private void Update()
    {
        if (isShowing)
        {
            RefreshMutedGameAudio();
        }

        if (!isShowing || !allowInputSkip)
        {
            return;
        }

        if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
        {
            RequestConfirm();
        }
    }

    public void ShowResult(bool isSuccess)
    {
        isShowing = true;
        isShowingSuccess = isSuccess;
        confirmRequested = false;

        StopCurrentVideo();
        StopFallback();
        MuteGameAudio();
        DisableResultButtons();
        ClearSelectedObject();

        SetObjectActive(missionSuccessPanel, isSuccess);
        SetObjectActive(missionFailPanel, !isSuccess);
        SetObjectsActive(successObjectsToHide, false);
        SetObjectsActive(failObjectsToHide, false);

        VideoPlayer nextVideoPlayer = isSuccess ? successVideoPlayer : failVideoPlayer;
        currentVideoPlayer = nextVideoPlayer;

        if (currentVideoPlayer != null)
        {
            currentVideoPlayer.loopPointReached -= HandleVideoEnded;
            currentVideoPlayer.loopPointReached += HandleVideoEnded;
            currentVideoPlayer.errorReceived -= HandleVideoError;
            currentVideoPlayer.errorReceived += HandleVideoError;
            float fallbackSeconds = isSuccess ? successFallbackSeconds : failFallbackSeconds;
            playRoutine = StartCoroutine(PlayVideoAfterPrepare(currentVideoPlayer, fallbackSeconds));
            return;
        }

        StartFallbackConfirm(isSuccess ? successFallbackSeconds : failFallbackSeconds);
    }

    private IEnumerator PlayVideoAfterPrepare(VideoPlayer videoPlayer, float fallbackSeconds)
    {
        if (videoPlayer == null)
        {
            StartFallbackConfirm(fallbackSeconds);
            yield break;
        }

        currentVideoHasError = false;
        ResetVideoPlayer(videoPlayer);
        ClearTargetTexture(videoPlayer);
        videoPlayer.Prepare();

        float timeoutAt = Time.realtimeSinceStartup + PrepareTimeoutSeconds;
        while (!videoPlayer.isPrepared && !currentVideoHasError && Time.realtimeSinceStartup < timeoutAt)
        {
            RefreshMutedGameAudio();
            yield return null;
        }

        if (!videoPlayer.isPrepared || currentVideoHasError)
        {
            StartFallbackConfirm(fallbackSeconds);
            playRoutine = null;
            yield break;
        }

        ResetVideoTime(videoPlayer);
        videoPlayer.Play();
        float playbackTimeoutAt = Time.realtimeSinceStartup + PlaybackStartTimeoutSeconds;
        while (!videoPlayer.isPlaying && !currentVideoHasError && Time.realtimeSinceStartup < playbackTimeoutAt)
        {
            RefreshMutedGameAudio();
            yield return null;
        }

        if (confirmWhenVideoEnds)
        {
            if (currentVideoHasError)
            {
                StartFallbackConfirm(fallbackSeconds);
                playRoutine = null;
                yield break;
            }

            float durationSeconds = GetExpectedPlaybackSeconds(videoPlayer, fallbackSeconds);
            float endAt = Time.realtimeSinceStartup + durationSeconds;
            while (videoPlayer != null &&
                   !confirmRequested &&
                   !currentVideoHasError &&
                   (videoPlayer.isPlaying || Time.realtimeSinceStartup < endAt))
            {
                RefreshMutedGameAudio();
                yield return null;
            }

            RequestConfirm();
            playRoutine = null;
            yield break;
        }

        StartFallbackConfirm(fallbackSeconds);
        playRoutine = null;
    }

    public void Hide()
    {
        isShowing = false;
        confirmRequested = false;
        StopCurrentVideo();
        StopFallback();
        RestoreGameAudio();
        RestoreResultButtons();
        SetObjectActive(missionSuccessPanel, false);
        SetObjectActive(missionFailPanel, false);
    }

    private void StartFallbackConfirm(float seconds)
    {
        StopFallback();
        if (seconds > 0f)
        {
            fallbackRoutine = StartCoroutine(FallbackConfirmRoutine(seconds));
        }
    }

    private IEnumerator FallbackConfirmRoutine(float seconds)
    {
        float endAt = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < endAt)
        {
            RefreshMutedGameAudio();
            yield return null;
        }

        RequestConfirm();
    }

    private void HandleVideoEnded(VideoPlayer source)
    {
        if (confirmWhenVideoEnds)
        {
            RequestConfirm();
        }
    }

    private void HandleVideoError(VideoPlayer source, string message)
    {
        currentVideoHasError = true;
        Debug.LogWarning($"[MissionResultVideoUI] Video playback failed: {message}");
    }

    private void RequestConfirm()
    {
        if (confirmRequested)
        {
            return;
        }

        confirmRequested = true;
        StopFallback();
        RestoreGameAudio();
        GameplayVideoAudioMuteController.ForceRestoreGameAudio();
        ConfirmRequested?.Invoke(isShowingSuccess);
    }

    private void DisableResultButtons()
    {
        RestoreResultButtons();

        Selectable[] successSelectables = missionSuccessPanel != null
            ? missionSuccessPanel.GetComponentsInChildren<Selectable>(true)
            : Array.Empty<Selectable>();
        Selectable[] failSelectables = missionFailPanel != null
            ? missionFailPanel.GetComponentsInChildren<Selectable>(true)
            : Array.Empty<Selectable>();

        disabledSelectables = new Selectable[successSelectables.Length + failSelectables.Length];
        disabledSelectableStates = new bool[disabledSelectables.Length];
        Array.Copy(successSelectables, 0, disabledSelectables, 0, successSelectables.Length);
        Array.Copy(failSelectables, 0, disabledSelectables, successSelectables.Length, failSelectables.Length);

        for (int i = 0; i < disabledSelectables.Length; i++)
        {
            Selectable selectable = disabledSelectables[i];
            if (selectable == null)
            {
                continue;
            }

            disabledSelectableStates[i] = selectable.interactable;
            selectable.interactable = false;
        }
    }

    private void RestoreResultButtons()
    {
        if (disabledSelectables == null || disabledSelectableStates == null)
        {
            return;
        }

        for (int i = 0; i < disabledSelectables.Length && i < disabledSelectableStates.Length; i++)
        {
            Selectable selectable = disabledSelectables[i];
            if (selectable != null)
            {
                selectable.interactable = disabledSelectableStates[i];
            }
        }

        disabledSelectables = null;
        disabledSelectableStates = null;
    }

    private static void ClearSelectedObject()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void StopCurrentVideo()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (currentVideoPlayer != null)
        {
            currentVideoPlayer.loopPointReached -= HandleVideoEnded;
            currentVideoPlayer.errorReceived -= HandleVideoError;
            currentVideoPlayer.Stop();
            currentVideoPlayer = null;
        }
    }

    private static void ResetVideoPlayer(VideoPlayer videoPlayer)
    {
        videoPlayer.gameObject.SetActive(true);
        videoPlayer.enabled = true;
        videoPlayer.Stop();
        videoPlayer.playbackSpeed = 1f;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = false;
    }

    private static void ResetVideoTime(VideoPlayer videoPlayer)
    {
        if (videoPlayer.canSetTime)
        {
            videoPlayer.time = 0d;
        }
    }

    private static void ClearTargetTexture(VideoPlayer videoPlayer)
    {
        RenderTexture targetTexture = videoPlayer.targetTexture;
        if (targetTexture == null)
        {
            return;
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = targetTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = previous;
    }

    private static float GetExpectedPlaybackSeconds(VideoPlayer videoPlayer, float fallbackSeconds)
    {
        if (videoPlayer.length > 0.1d)
        {
            return (float)videoPlayer.length + 0.25f;
        }

        return Mathf.Max(0.1f, fallbackSeconds);
    }

    private void MuteGameAudio()
    {
        if (audioMuted)
        {
            return;
        }

        audioMuted = true;
        Transform successRoot = missionSuccessPanel != null ? missionSuccessPanel.transform : null;
        Transform failRoot = missionFailPanel != null ? missionFailPanel.transform : null;
        GameplayVideoAudioMuteController.MuteGameAudio(successRoot, failRoot);
    }

    private void RefreshMutedGameAudio()
    {
        if (!audioMuted)
        {
            return;
        }

        Transform successRoot = missionSuccessPanel != null ? missionSuccessPanel.transform : null;
        Transform failRoot = missionFailPanel != null ? missionFailPanel.transform : null;
        GameplayVideoAudioMuteController.RefreshGameAudioMute(successRoot, failRoot);
    }

    private void RestoreGameAudio()
    {
        if (!audioMuted)
        {
            return;
        }

        audioMuted = false;
        GameplayVideoAudioMuteController.RestoreGameAudio();
    }

    private void StopFallback()
    {
        if (fallbackRoutine == null)
        {
            return;
        }

        StopCoroutine(fallbackRoutine);
        fallbackRoutine = null;
    }

    private void AutoBindPanels()
    {
        missionSuccessPanel = missionSuccessPanel != null ? missionSuccessPanel : FindChildGameObject("MissionSuccessPanel");
        missionFailPanel = missionFailPanel != null ? missionFailPanel : FindChildGameObject("MissionFailPanel");
    }

    private GameObject FindChildGameObject(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
            {
                return children[i].gameObject;
            }
        }

        return null;
    }

    private static void SetObjectActive(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    private static void SetObjectsActive(GameObject[] targets, bool isActive)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            SetObjectActive(targets[i], isActive);
        }
    }
}
