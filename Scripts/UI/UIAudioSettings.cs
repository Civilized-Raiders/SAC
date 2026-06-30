using System;
using UnityEngine;

public static class UIAudioSettings
{
    public const float DefaultVolume = 1f;

    private const string BgmVolumeKey = "UIAudioSettings.BgmVolume";
    private const string SfxVolumeKey = "UIAudioSettings.SfxVolume";

    public static event Action<float, float> Applied;
    public static event Action<float, float> Previewed;

    public static float BgmVolume => PlayerPrefs.GetFloat(GetScopedKey(BgmVolumeKey), DefaultVolume);
    public static float SfxVolume => PlayerPrefs.GetFloat(GetScopedKey(SfxVolumeKey), DefaultVolume);

    public static void Save(float bgmVolume, float sfxVolume)
    {
        float safeBgmVolume = Mathf.Clamp01(bgmVolume);
        float safeSfxVolume = Mathf.Clamp01(sfxVolume);

        PlayerPrefs.SetFloat(GetScopedKey(BgmVolumeKey), safeBgmVolume);
        PlayerPrefs.SetFloat(GetScopedKey(SfxVolumeKey), safeSfxVolume);
        PlayerPrefs.Save();

        // UI는 개인 로컬 음량 설정만 저장한다.
        // 실제 AudioMixer 연결은 사운드 시스템이 준비되면 이 이벤트를 받아 처리한다.
        Applied?.Invoke(safeBgmVolume, safeSfxVolume);
    }

    public static void Preview(float bgmVolume, float sfxVolume)
    {
        Previewed?.Invoke(Mathf.Clamp01(bgmVolume), Mathf.Clamp01(sfxVolume));
    }

    public static void PreviewSaved()
    {
        Preview(BgmVolume, SfxVolume);
    }

    private static string GetScopedKey(string baseKey)
    {
        string playerScope = GetPlayerScopeSuffix();
        return string.IsNullOrEmpty(playerScope) ? baseKey : $"{baseKey}.{playerScope}";
    }

    private static string GetPlayerScopeSuffix()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == "-name")
            {
                string playerName = arguments[i + 1];
                if (!string.IsNullOrWhiteSpace(playerName))
                {
                    return playerName.Trim();
                }
            }
        }

        return string.Empty;
    }

    public static string ToPercentText(float volume)
    {
        int percent = Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f);
        return $"{percent}%";
    }
}
