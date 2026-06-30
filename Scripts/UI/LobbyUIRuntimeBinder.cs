using UnityEngine;
using UnityEngine.SceneManagement;

public static class LobbyUIRuntimeBinder
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BindCurrentScene()
    {
        Transform titlePanel = FindInLoadedScene("TitlePanel");
        Transform createRoomPanel = FindInLoadedScene("CreateRoomPanel");
        Transform joinRoomPanel = FindInLoadedScene("JoinRoomPanel");
        Transform roomPanel = FindInLoadedScene("RoomPanel");
        Transform settingsPanel = FindInLoadedScene("SettingsPanel");

        if (titlePanel == null && createRoomPanel == null && joinRoomPanel == null && roomPanel == null && settingsPanel == null)
        {
            return;
        }

        Canvas canvas = FindCanvas(titlePanel, createRoomPanel, joinRoomPanel, roomPanel, settingsPanel);
        if (canvas == null)
        {
            return;
        }

        AddPanelComponent<TitlePanel>(titlePanel);
        AddPanelComponent<CreateRoomPanel>(createRoomPanel);
        AddPanelComponent<JoinRoomPanel>(joinRoomPanel);
        AddPanelComponent<RoomPanel>(roomPanel);
        AddPanelComponent<SettingsPanel>(settingsPanel);

        if (canvas.GetComponent<UIFlowController>() == null && titlePanel != null)
        {
            canvas.gameObject.AddComponent<UIFlowController>();
        }

        if (titlePanel != null)
        {
            titlePanel.gameObject.SetActive(true);
        }

        SetActive(createRoomPanel, false);
        SetActive(joinRoomPanel, false);
        SetActive(settingsPanel, false);
    }

    private static Canvas FindCanvas(params Transform[] panels)
    {
        foreach (Transform panel in panels)
        {
            if (panel == null)
            {
                continue;
            }

            Canvas canvas = panel.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                return canvas;
            }
        }

        return null;
    }

    private static void AddPanelComponent<T>(Transform panel) where T : Component
    {
        if (panel != null && panel.GetComponent<T>() == null)
        {
            panel.gameObject.AddComponent<T>();
        }
    }

    private static void SetActive(Transform panel, bool active)
    {
        if (panel != null)
        {
            panel.gameObject.SetActive(active);
        }
    }

    private static Transform FindInLoadedScene(string objectName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform transform in allTransforms)
        {
            if (transform.name != objectName)
            {
                continue;
            }

            Scene scene = transform.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                return transform;
            }
        }

        return null;
    }
}
