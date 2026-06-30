using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissionResultRuntimeBinder
{
    private static bool isInstalled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (isInstalled)
        {
            return;
        }

        isInstalled = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        BindCurrentScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindCurrentScene();
    }

    private static void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        BindCurrentScene();
    }

    private static void BindCurrentScene()
    {
        Transform gameplayHudRoot = FindInLoadedScene("GameplayHUDRoot");
        Transform missionResultRoot = FindInLoadedScene("MissionResultRoot");

        if (gameplayHudRoot == null || missionResultRoot == null)
        {
            return;
        }

        Canvas canvas = gameplayHudRoot.GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            return;
        }

        if (canvas.GetComponent<MissionResultBridge>() == null)
        {
            canvas.gameObject.AddComponent<MissionResultBridge>();
        }
    }

    private static Transform FindInLoadedScene(string objectName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform transform = allTransforms[i];
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
