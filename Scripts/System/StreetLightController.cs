using System.Collections.Generic;
using UnityEngine;

/* =========================================================
 * [Modification History]
 * 2026-06-16 : 가로등(Point/Spot Light) ON/OFF
 *              - DayNightCycle.ShouldStreetLightsBeOn 기준으로 저녁~새벽에만 켬
 * 2026-06-16 : 원본 Intensity 유지 (에셋 가로등은 400 등 큰 값 사용)
 * 2026-06-28 : 고정 저녁 — 항상 ON, dim/flicker 그룹을 이 스크립트에서만 관리
 * ========================================================= */

public class StreetLightController : MonoBehaviour
{
    [Header("Lights")]
    [Tooltip("비워 두면 이 오브젝트와 자식에서 Light를 자동 수집")]
    [SerializeField] private Light[] streetLights;
    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Dim Lights")]
    [Tooltip("여기에 넣은 Light는 원본 Intensity × 배율로 약하게 켜짐")]
    [SerializeField] private Light[] dimLights;
    [SerializeField] [Range(0.05f, 1f)] private float dimIntensityMultiplier = 0.45f;

    [Header("Flicker Lights")]
    [Tooltip("여기에 넣은 Light는 밝기가 흔들림. dimLights와 겹치면 약한 밝기를 기준으로 깜빡임")]
    [SerializeField] private Light[] flickerLights;
    [SerializeField] private float flickerSpeed = 8f;
    [SerializeField] [Range(0f, 1f)] private float flickerMinMultiplier = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float flickerMaxMultiplier = 1f;

    private readonly Dictionary<Light, float> originalIntensities = new();
    private readonly HashSet<Light> dimLightSet = new();
    private readonly HashSet<Light> flickerLightSet = new();
    private readonly Dictionary<Light, float> flickerSeeds = new();

    private void Awake()
    {
        if (streetLights == null || streetLights.Length == 0)
        {
            streetLights = GetComponentsInChildren<Light>(includeInactiveChildren);
        }

        CacheLightGroups();
    }

    private void Start()
    {
        EnableAllLights();
        ApplyStaticIntensities();
    }

    private void Update()
    {
        UpdateFlickerLights();
    }

    private void CacheLightGroups()
    {
        originalIntensities.Clear();
        dimLightSet.Clear();
        flickerLightSet.Clear();
        flickerSeeds.Clear();

        RegisterLights(streetLights);
        RegisterLights(dimLights, dimLightSet);
        RegisterLights(flickerLights, flickerLightSet);

        foreach (Light light in flickerLightSet)
        {
            flickerSeeds[light] = Random.Range(0f, 100f);
        }
    }

    private void RegisterLights(IReadOnlyList<Light> lights, HashSet<Light> group = null)
    {
        if (lights == null) return;

        for (int i = 0; i < lights.Count; i++)
        {
            Light light = lights[i];
            if (light == null) continue;
            if (light.type != LightType.Point && light.type != LightType.Spot) continue;

            originalIntensities.TryAdd(light, light.intensity);
            group?.Add(light);
        }
    }

    private void EnableAllLights()
    {
        foreach (Light light in originalIntensities.Keys)
        {
            if (light == null) continue;
            light.enabled = true;
        }
    }

    private void ApplyStaticIntensities()
    {
        foreach (KeyValuePair<Light, float> entry in originalIntensities)
        {
            Light light = entry.Key;
            if (light == null || flickerLightSet.Contains(light)) continue;

            light.intensity = GetBaseIntensity(light, entry.Value);
        }
    }

    private void UpdateFlickerLights()
    {
        if (flickerLightSet.Count == 0) return;

        float time = Time.time * flickerSpeed;

        foreach (Light light in flickerLightSet)
        {
            if (light == null || !light.enabled) continue;

            float original = originalIntensities.GetValueOrDefault(light, light.intensity);
            float baseIntensity = GetBaseIntensity(light, original);
            float seed = flickerSeeds.GetValueOrDefault(light, 0f);
            float noise = Mathf.PerlinNoise(time + seed, seed * 0.37f);
            float multiplier = Mathf.Lerp(flickerMinMultiplier, flickerMaxMultiplier, noise);

            light.intensity = baseIntensity * multiplier;
        }
    }

    private float GetBaseIntensity(Light light, float originalIntensity)
    {
        if (dimLightSet.Contains(light))
        {
            return originalIntensity * dimIntensityMultiplier;
        }

        return originalIntensity;
    }
}
