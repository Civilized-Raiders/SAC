using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class RoomListItemUI : MonoBehaviour
{
    [Header("Room Info")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text roomPlayerCountText;
    [SerializeField] private TMP_Text roomPublicStateText;

    [Header("Actions")]
    [SerializeField] private Button joinRoomButton;

    private string roomCode;

    public string RoomCode => roomCode;

    public void SetRoomInfo(string roomCode, int currentPlayerCount, int maxPlayerCount, bool isPublicRoom)
    {
        // 방 목록 카드는 Fusion에서 받은 방 정보를 표시만 하고, 방 생성/입장 확정은 하지 않습니다.
        this.roomCode = roomCode;

        SetText(roomNameText, roomCode);

        bool hasPlayerCount = currentPlayerCount >= 0;
        bool isFullRoom = false;

        if (!hasPlayerCount)
        {
            SetText(roomPlayerCountText, "-/4");
        }
        else
        {
            int safeMaxPlayerCount = Mathf.Max(1, maxPlayerCount);
            int safeCurrentPlayerCount = Mathf.Clamp(currentPlayerCount, 0, safeMaxPlayerCount);
            isFullRoom = safeCurrentPlayerCount >= safeMaxPlayerCount;
            SetText(roomPlayerCountText, $"{safeCurrentPlayerCount}/{safeMaxPlayerCount}");
        }

        SetText(roomPublicStateText, isFullRoom ? "가득참" : isPublicRoom ? "공개방" : "비공개방");
        SetInteractable(!isFullRoom);
    }

    public void SetJoinAction(UnityAction joinAction)
    {
        if (joinRoomButton == null)
        {
            return;
        }

        joinRoomButton.onClick.RemoveAllListeners();
        joinRoomButton.onClick.AddListener(joinAction);
    }

    public void SetInteractable(bool isInteractable)
    {
        if (joinRoomButton != null)
        {
            joinRoomButton.interactable = isInteractable;
        }
    }

    private static void SetText(TMP_Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }
}
