using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class GameplayIntroVideoUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject introRoot;

    [Header("Video")]
    [SerializeField] private VideoPlayer introVideoPlayer;
    [SerializeField] private float displaySeconds = 2f;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool blockInputWhileShowing = true;

    private const float PrepareTimeoutSeconds = 10f;
    private const float PlaybackStartTimeoutSeconds = 5f;
    private const float GameManagerWaitTimeoutSeconds = 3f;

    private Coroutine playRoutine;
    private bool inputBlocked;
    private bool timerPaused;
    private bool audioMuted;

    private void Awake()
    {
        AutoBindReferences();
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayIntro();
            return;
        }

        SetRootActive(false);
    }

    private void OnDisable()
    {
        StopIntro();
    }

    public void PlayIntro()
    {
        StopPlayback(false);
        SetRootActive(true);
        playRoutine = StartCoroutine(PlayIntroRoutine());
    }

    public void StopIntro()
    {
        StopPlayback(true);
    }

    private void StopPlayback(bool hideRoot)
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (introVideoPlayer != null)
        {
            introVideoPlayer.Stop();
        }

        ReleaseInputBlock();
        RestoreGameAudio();
        if (timerPaused)
        {
            SetGameplayTimerPaused(false);
        }

        if (hideRoot)
        {
            SetRootActive(false);
        }
    }

    private IEnumerator PlayIntroRoutine()
    {
        SetRootActive(true);
        BringRootToFront();
        ApplyInputBlock();
        MuteGameAudio();
        yield return SetGameplayTimerPausedWhenReady(true);

        yield return PlayVideoAfterPrepare(introVideoPlayer, Mathf.Max(0f, displaySeconds));

        if (introVideoPlayer != null)
        {
            introVideoPlayer.Stop();
        }

        ReleaseInputBlock();
        RestoreGameAudio();
        SetGameplayTimerPaused(false);
        SetRootActive(false);
        playRoutine = null;
    }

    private IEnumerator PlayVideoAfterPrepare(VideoPlayer videoPlayer, float seconds)
    {
        if (videoPlayer == null)
        {
            float waitEndAt = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < waitEndAt)
            {
                RefreshMutedGameAudio();
                yield return null;
            }

            yield break;
        }

        videoPlayer.Stop();
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = false;
        ClearTargetTexture(videoPlayer);
        videoPlayer.Prepare();

        float timeoutAt = Time.realtimeSinceStartup + PrepareTimeoutSeconds;
        while (!videoPlayer.isPrepared && Time.realtimeSinceStartup < timeoutAt)
        {
            RefreshMutedGameAudio();
            yield return null;
        }

        if (!videoPlayer.isPrepared)
        {
            float fallbackEndAt = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < fallbackEndAt)
            {
                RefreshMutedGameAudio();
                yield return null;
            }

            yield break;
        }

        if (videoPlayer.canSetTime)
        {
            videoPlayer.time = 0d;
        }

        videoPlayer.Play();
        float playbackTimeoutAt = Time.realtimeSinceStartup + PlaybackStartTimeoutSeconds;
        while (!videoPlayer.isPlaying && Time.realtimeSinceStartup < playbackTimeoutAt)
        {
            RefreshMutedGameAudio();
            yield return null;
        }

        float endAt = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < endAt)
        {
            RefreshMutedGameAudio();
            yield return null;
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

    private void ApplyInputBlock()
    {
        if (!blockInputWhileShowing || inputBlocked)
        {
            return;
        }

        inputBlocked = true;
        GameplayInputGuard.SetBlocked(true);
    }

    private void ReleaseInputBlock()
    {
        if (!inputBlocked)
        {
            return;
        }

        inputBlocked = false;
        GameplayInputGuard.SetBlocked(false);
    }

    private IEnumerator SetGameplayTimerPausedWhenReady(bool isPaused)
    {
        float timeoutAt = Time.realtimeSinceStartup + GameManagerWaitTimeoutSeconds;
        while (NetworkGameManager.Instance == null && Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
        }

        SetGameplayTimerPaused(isPaused);
    }

    private void SetGameplayTimerPaused(bool isPaused)
    {
        NetworkGameManager gameManager = NetworkGameManager.Instance;
        if (gameManager == null)
        {
            timerPaused = false;
            return;
        }

        gameManager.SetTimerPaused(isPaused);
        timerPaused = isPaused;
    }

    private void MuteGameAudio()
    {
        if (audioMuted)
        {
            return;
        }

        audioMuted = true;
        Transform excludedRoot = introRoot != null ? introRoot.transform : transform;
        GameplayVideoAudioMuteController.MuteGameAudio(excludedRoot);
    }

    private void RefreshMutedGameAudio()
    {
        if (!audioMuted)
        {
            return;
        }

        Transform excludedRoot = introRoot != null ? introRoot.transform : transform;
        GameplayVideoAudioMuteController.RefreshGameAudioMute(excludedRoot);
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

    private void AutoBindReferences()
    {
        introRoot = introRoot != null ? introRoot : gameObject;
        introVideoPlayer = introVideoPlayer != null ? introVideoPlayer : GetComponentInChildren<VideoPlayer>(true);
    }

    private void SetRootActive(bool isActive)
    {
        if (introRoot != null)
        {
            introRoot.SetActive(isActive);
        }
    }

    private void BringRootToFront()
    {
        if (introRoot != null)
        {
            introRoot.transform.SetAsLastSibling();
        }
    }
}
