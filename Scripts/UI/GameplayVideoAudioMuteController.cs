using System.Collections.Generic;
using UnityEngine;

public static class GameplayVideoAudioMuteController
{
    private struct AudioMuteState
    {
        public AudioSource source;
        public bool wasMuted;
    }

    private static readonly List<AudioMuteState> mutedSources = new List<AudioMuteState>();
    private static int muteRequestCount;

    public static void MuteGameAudio(params Transform[] excludedRoots)
    {
        muteRequestCount++;
        if (muteRequestCount > 1)
        {
            return;
        }

        mutedSources.Clear();
        RefreshGameAudioMute(excludedRoots);
    }

    public static void RefreshGameAudioMute(params Transform[] excludedRoots)
    {
        if (muteRequestCount <= 0)
        {
            return;
        }

        AudioSource[] audioSources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < audioSources.Length; i++)
        {
            AudioSource source = audioSources[i];
            if (source == null || IsAlreadyTracked(source) || IsUnderExcludedRoot(source.transform, excludedRoots))
            {
                continue;
            }

            mutedSources.Add(new AudioMuteState
            {
                source = source,
                wasMuted = source.mute
            });
            source.mute = true;
        }
    }

    public static void RestoreGameAudio()
    {
        if (muteRequestCount <= 0)
        {
            return;
        }

        muteRequestCount--;
        if (muteRequestCount > 0)
        {
            return;
        }

        for (int i = 0; i < mutedSources.Count; i++)
        {
            AudioMuteState state = mutedSources[i];
            if (state.source != null)
            {
                state.source.mute = state.wasMuted;
            }
        }

        mutedSources.Clear();
    }

    public static void ForceRestoreGameAudio()
    {
        for (int i = 0; i < mutedSources.Count; i++)
        {
            AudioMuteState state = mutedSources[i];
            if (state.source != null)
            {
                state.source.mute = state.wasMuted;
            }
        }

        mutedSources.Clear();
        muteRequestCount = 0;
    }

    private static bool IsAlreadyTracked(AudioSource source)
    {
        for (int i = 0; i < mutedSources.Count; i++)
        {
            if (mutedSources[i].source == source)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnderExcludedRoot(Transform target, Transform[] excludedRoots)
    {
        if (target == null || excludedRoots == null)
        {
            return false;
        }

        for (int i = 0; i < excludedRoots.Length; i++)
        {
            Transform excludedRoot = excludedRoots[i];
            if (excludedRoot != null && target.IsChildOf(excludedRoot))
            {
                return true;
            }
        }

        return false;
    }
}
