using UnityEngine;
using UnityEngine.SceneManagement;

// Subscribes to FusionRoomManager.OnRoomShutdown and loads Title scene when a room ends.
public class RoomShutdownHandler : MonoBehaviour
{
    private bool isQuitting = false;

    private void Start()
    {
        if (FusionRoomManager.Instance != null)
            FusionRoomManager.Instance.OnRoomShutdown += HandleRoomShutdown;
    }

    private void OnDestroy()
    {
        if (FusionRoomManager.Instance != null)
            FusionRoomManager.Instance.OnRoomShutdown -= HandleRoomShutdown;
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void HandleRoomShutdown()
    {
        if (isQuitting) return;

        // Fusion NetworkRunner.Shutdown는 비동기이므로 Runner가 완전히 정리될 때까지 대기 후 씬 전환
        StartCoroutine(LoadTitleAfterShutdown());
    }

    private System.Collections.IEnumerator LoadTitleAfterShutdown()
    {
        // 최대 대기 프레임 수
        int maxFrames = 120;
        int waited = 0;

        while (waited < maxFrames)
        {
            // FusionRoomManager.Instance may be null if manager was destroyed; break in that case
            if (FusionRoomManager.Instance == null)
                break;

            // Wait until Runner becomes null (Shutdown finished)
            if (FusionRoomManager.Instance.Runner == null)
                break;

            waited++;
            yield return null;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameplayInputGuard.ResetBlocked();
        GameplayVideoAudioMuteController.ForceRestoreGameAudio();

        SceneManager.LoadScene("Title");
    }
}
