using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class LobbyUIRuntimeBinder
{
    // 자동 바인딩이 동작할 씬 화이트리스트.
    // - RoomLobby / 인게임 씬에 같은 이름(TitlePanel 등)의 오브젝트가 생기더라도
    //   잘못 잡혀서 컴포넌트가 추가되거나 활성화되는 사고를 막는다.
    // - Fusion 도입 후 NetworkObject가 같은 이름을 쓰는 경우에도 안전.
    // 새 로비 씬을 추가하면 여기에 씬 이름을 등록할 것.
    private static readonly string[] AllowedScenes = { "Title" , "Room" , "Gameplay" , "Result" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BindCurrentScene()
    {
        if (!IsAllowedScene())
        {
            return;
        }


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

    private static bool IsAllowedScene()
    {
        string activeName = SceneManager.GetActiveScene().name;
        for (int i = 0; i < AllowedScenes.Length; i++)
        {
            if (activeName == AllowedScenes[i])
            {
                return true;
            }
        }
        return false;
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
