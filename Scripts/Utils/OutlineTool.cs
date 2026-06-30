using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class OutlineTool
{
    public enum OutlineColorType
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Black,
        White
    }

    private const string ProxyPrefix = "__OutlineProxy__";
    private const float DefaultWidth = 0.03f;

    private static readonly Dictionary<GameObject, OutlineData> OutlineMap = new();

    public static void Show(GameObject target, OutlineColorType colorType, float width = DefaultWidth, bool includeChildren = true)
    {
        if (target == null)
        {
            Debug.LogWarning("[OutlineTool] Show called with null target.");
            return;
        }

        if (!OutlineMap.TryGetValue(target, out OutlineData data) || !data.IsValid)
        {
            Debug.Log($"[OutlineTool] Building outline for {target.name}");
            data = BuildOutline(target, includeChildren);

            if (data == null)
            {
                Debug.LogWarning($"[OutlineTool] Build failed for {target.name}");
                return;
            }

            OutlineMap[target] = data;
        }

        ApplyVisual(data, colorType, width);
        Debug.Log($"[OutlineTool] Show {target.name} | Color: {colorType} | Width: {width} | ProxyCount: {data.ProxyRenderers.Count}");

        for (int i = 0; i < data.ProxyRenderers.Count; i++)
        {
            Renderer renderer = data.ProxyRenderers[i];

            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
    }

    public static void Hide(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("[OutlineTool] Hide called with null target.");
            return;
        }

        if (!OutlineMap.TryGetValue(target, out OutlineData data))
        {
            Debug.LogWarning($"[OutlineTool] Hide skipped. No outline data for {target.name}");
            return;
        }

        if (!data.IsValid)
        {
            Clear(target);
            return;
        }

        for (int i = 0; i < data.ProxyRenderers.Count; i++)
        {
            Renderer renderer = data.ProxyRenderers[i];

            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        Debug.Log($"[OutlineTool] Hide {target.name} | ProxyCount: {data.ProxyRenderers.Count}");
    }

    public static void Clear(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (!OutlineMap.TryGetValue(target, out OutlineData data))
        {
            return;
        }

        for (int i = 0; i < data.ProxyObjects.Count; i++)
        {
            if (data.ProxyObjects[i] != null)
            {
                Object.Destroy(data.ProxyObjects[i]);
            }
        }

        if (data.Material != null)
        {
            Object.Destroy(data.Material);
        }

        OutlineMap.Remove(target);
    }

    private static OutlineData BuildOutline(GameObject target, bool includeChildren)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            Debug.LogWarning($"[OutlineTool] No compatible outline shader found on {target.name}");
            return null;
        }

        Material material = new(shader)
        {
            name = $"OutlineRuntime_{target.name}",
            enableInstancing = true,
            renderQueue = (int)RenderQueue.Geometry - 1
        };

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Front);
        }

        Renderer[] renderers = includeChildren
            ? target.GetComponentsInChildren<Renderer>(true)
            : target.GetComponents<Renderer>();

        OutlineData data = new(material);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer source = renderers[i];

            if (source == null || source.transform.name.StartsWith(ProxyPrefix))
            {
                continue;
            }

            Renderer proxyRenderer = CreateProxy(source, material);

            if (proxyRenderer == null)
            {
                Debug.LogWarning($"[OutlineTool] Proxy creation failed for renderer on {source.name}");
                continue;
            }

            data.ProxyRenderers.Add(proxyRenderer);
            data.ProxyObjects.Add(proxyRenderer.gameObject);
        }

        Debug.Log($"[OutlineTool] Build complete for {target.name} | RendererCount: {renderers.Length} | ProxyCount: {data.ProxyRenderers.Count}");
        return data.ProxyRenderers.Count > 0 ? data : null;
    }

    private static Renderer CreateProxy(Renderer source, Material material)
    {
        GameObject proxy = new($"{ProxyPrefix}{source.name}");
        proxy.hideFlags = HideFlags.HideAndDontSave;
        proxy.layer = source.gameObject.layer;
        proxy.transform.SetParent(source.transform, false);

        if (source is MeshRenderer meshRenderer)
        {
            MeshFilter sourceFilter = meshRenderer.GetComponent<MeshFilter>();

            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                Debug.LogWarning($"[OutlineTool] MeshRenderer has no valid MeshFilter on {source.name}");
                Object.Destroy(proxy);
                return null;
            }

            MeshFilter proxyFilter = proxy.AddComponent<MeshFilter>();
            proxyFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer proxyRenderer = proxy.AddComponent<MeshRenderer>();
            proxyRenderer.sharedMaterial = material;
            SetupRenderer(proxyRenderer);
            return proxyRenderer;
        }

        if (source is SkinnedMeshRenderer skinnedRenderer)
        {
            if (skinnedRenderer.sharedMesh == null)
            {
                Debug.LogWarning($"[OutlineTool] SkinnedMeshRenderer has no shared mesh on {source.name}");
                Object.Destroy(proxy);
                return null;
            }

            SkinnedMeshRenderer proxyRenderer = proxy.AddComponent<SkinnedMeshRenderer>();
            proxyRenderer.sharedMesh = skinnedRenderer.sharedMesh;
            proxyRenderer.sharedMaterial = material;
            proxyRenderer.rootBone = skinnedRenderer.rootBone;
            proxyRenderer.bones = skinnedRenderer.bones;
            proxyRenderer.localBounds = skinnedRenderer.localBounds;
            proxyRenderer.updateWhenOffscreen = skinnedRenderer.updateWhenOffscreen;
            SetupRenderer(proxyRenderer);
            return proxyRenderer;
        }

        Object.Destroy(proxy);
        return null;
    }

    private static void SetupRenderer(Renderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.enabled = false;
    }

    private static void ApplyVisual(OutlineData data, OutlineColorType colorType, float width)
    {
        float finalWidth = Mathf.Max(0.001f, width);
        Color color = GetColor(colorType);

        Debug.Log($"[OutlineTool] ApplyVisual | Color: {colorType} | FinalWidth: {finalWidth}");

        if (data.Material.HasProperty("_BaseColor"))
        {
            data.Material.SetColor("_BaseColor", color);
        }

        if (data.Material.HasProperty("_Color"))
        {
            data.Material.SetColor("_Color", color);
        }

        Vector3 scale = Vector3.one * (1f + finalWidth);

        for (int i = 0; i < data.ProxyObjects.Count; i++)
        {
            GameObject proxyObject = data.ProxyObjects[i];

            if (proxyObject != null)
            {
                proxyObject.transform.localScale = scale;
            }
        }
    }

    private static Color GetColor(OutlineColorType colorType)
    {
        return colorType switch
        {
            OutlineColorType.Red => Color.red,
            OutlineColorType.Blue => Color.blue,
            OutlineColorType.Green => Color.green,
            OutlineColorType.Yellow => Color.yellow,
            OutlineColorType.Purple => new Color(0.6f, 0f, 1f, 1f),
            OutlineColorType.Black => Color.black,
            OutlineColorType.White => Color.white,
            _ => Color.yellow
        };
    }

    private sealed class OutlineData
    {
        public Material Material { get; }
        public List<Renderer> ProxyRenderers { get; } = new();
        public List<GameObject> ProxyObjects { get; } = new();

        public bool IsValid => Material != null;

        public OutlineData(Material material)
        {
            Material = material;
        }
    }
}
