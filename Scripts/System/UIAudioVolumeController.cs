using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIAudioVolumeController : MonoBehaviour
{
    private const float BgmInitDurationSeconds = 3f;

    [Header("Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Playback")]
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private bool playBgmOnStart = true;

    private Coroutine bgmInitRoutine;

    private void Awake()
    {
        ResolveAudioSources();

        if (clickClip == null && sfxSource != null)
        {
            clickClip = sfxSource.clip;
        }

        ApplySavedVolumes();
    }

    private void OnEnable()
    {
        GameplayVideoAudioMuteController.ForceRestoreGameAudio();
        UIAudioSettings.Previewed += ApplyVolumes;
        UIAudioSettings.Applied += ApplyVolumes;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplySavedVolumes();
        RestartBgmInitialization();
    }

    private void OnDisable()
    {
        UIAudioSettings.Previewed -= ApplyVolumes;
        UIAudioSettings.Applied -= ApplyVolumes;
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (bgmInitRoutine != null)
        {
            StopCoroutine(bgmInitRoutine);
            bgmInitRoutine = null;
        }
    }

    private void Start()
    {
        RestartBgmInitialization();
    }

    public void PlayButtonClick()
    {
        if (clickClip == null)
        {
            return;
        }

        float sfxVolume = UIAudioSettings.SfxVolume;

        if (sfxSource != null && sfxSource != bgmSource)
        {
            sfxSource.PlayOneShot(clickClip);
            return;
        }

        if (bgmSource != null)
        {
            bgmSource.PlayOneShot(clickClip, sfxVolume);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameplayVideoAudioMuteController.ForceRestoreGameAudio();
        RestartBgmInitialization();
    }

    private void RestartBgmInitialization()
    {
        if (bgmInitRoutine != null)
        {
            StopCoroutine(bgmInitRoutine);
        }

        bgmInitRoutine = StartCoroutine(InitializeBgmRoutine());
    }

    private void ResolveAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length == 0)
        {
            return;
        }

        if (bgmSource == null)
        {
            bgmSource = FindLoopingSource(sources) ?? sources[0];
        }

        if (sfxSource == null || sfxSource == bgmSource)
        {
            sfxSource = FindAlternateSource(sources, bgmSource);
        }
    }

    private static AudioSource FindLoopingSource(AudioSource[] sources)
    {
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null && sources[i].loop)
            {
                return sources[i];
            }
        }

        return null;
    }

    private static AudioSource FindAlternateSource(AudioSource[] sources, AudioSource primarySource)
    {
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null && sources[i] != primarySource)
            {
                return sources[i];
            }
        }

        return null;
    }

    private IEnumerator InitializeBgmRoutine()
    {
        float elapsed = 0f;

        while (elapsed < BgmInitDurationSeconds)
        {
            float bgmVolume = UIAudioSettings.BgmVolume;
            float sfxVolume = UIAudioSettings.SfxVolume;
            ApplyVolumes(bgmVolume, sfxVolume);

            if (playBgmOnStart && bgmSource != null && bgmSource.clip != null && !bgmSource.isPlaying)
            {
                bgmSource.Play();
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 사운드 관련 추가: Fusion 비동기 씬 로드가 Start 이후에 BGM을 켜는 경우를 대비한다.
        yield return new WaitForSecondsRealtime(0.5f);
        ApplySavedVolumes();
        yield return new WaitForSecondsRealtime(0.5f);
        ApplySavedVolumes();

        bgmInitRoutine = null;
    }

    private void ApplySavedVolumes()
    {
        ApplyVolumes(UIAudioSettings.BgmVolume, UIAudioSettings.SfxVolume);
    }

    private void ApplyVolumes(float bgmVolume, float sfxVolume)
    {
        if (bgmSource != null)
        {
            bgmSource.volume = Mathf.Clamp01(bgmVolume);
        }

        if (sfxSource != null && sfxSource != bgmSource)
        {
            sfxSource.volume = Mathf.Clamp01(sfxVolume);
        }
    }
}
