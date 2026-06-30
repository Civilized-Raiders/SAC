using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ResultPlayerSlotUI : MonoBehaviour
{
    public enum ResultPlayerState
    {
        Empty,
        Alive,
        Dead
    }

    [Header("Slot State")]
    [SerializeField] private Image slotBackgroundImage;
    [SerializeField] private Sprite aliveSlotSprite;
    [SerializeField] private Sprite deadSlotSprite;
    [SerializeField] private Sprite emptySlotSprite;
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color deadColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Player Info")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Image playerPortraitImage;

    [Header("Result Values")]
    [SerializeField] private TextMeshProUGUI survivalTimeValueText;
    [SerializeField] private TextMeshProUGUI collectedItemCountText;
    [FormerlySerializedAs("monsterKillCountText")]
    [SerializeField] private TextMeshProUGUI specialArtifactCountText;

    private void Start()
    {
        // 플레이어 결과 슬롯 UI는 확정된 플레이어별 결과값을 표시만 담당한다.
        // 생존 여부, 회수 수량, 특수 유물 수는 UI에서 직접 계산하지 않는다.
        if (slotBackgroundImage == null)
        {
            slotBackgroundImage = GetComponent<Image>();
        }
    }

    public void SetPlayerResult(
        string playerName,
        Sprite portrait,
        ResultPlayerState state,
        string survivalTimeText,
        int collectedItemCount,
        int specialArtifactCount)
    {
        SetState(state);
        SetText(playerNameText, string.IsNullOrEmpty(playerName) ? "-" : playerName);
        SetPortrait(portrait, state != ResultPlayerState.Empty);
        SetText(survivalTimeValueText, string.IsNullOrEmpty(survivalTimeText) ? "00:00" : survivalTimeText);
        SetText(collectedItemCountText, $"{Mathf.Max(0, collectedItemCount)}");
        SetText(specialArtifactCountText, $"{Mathf.Max(0, specialArtifactCount)}");
    }

    public void SetEmpty()
    {
        SetState(ResultPlayerState.Empty);
        SetText(playerNameText, "-");
        SetPortrait(null, false);
        SetText(survivalTimeValueText, "00:00");
        SetText(collectedItemCountText, "0");
        SetText(specialArtifactCountText, "0");
    }

    public void SetState(ResultPlayerState state)
    {
        if (slotBackgroundImage == null)
        {
            return;
        }

        Sprite nextSprite = GetStateSprite(state);
        if (nextSprite != null)
        {
            slotBackgroundImage.sprite = nextSprite;
        }

        slotBackgroundImage.color = GetStateColor(state);
    }

    private Sprite GetStateSprite(ResultPlayerState state)
    {
        switch (state)
        {
            case ResultPlayerState.Alive:
                return aliveSlotSprite;
            case ResultPlayerState.Dead:
                return deadSlotSprite;
            case ResultPlayerState.Empty:
                return emptySlotSprite != null ? emptySlotSprite : aliveSlotSprite;
            default:
                return null;
        }
    }

    private Color GetStateColor(ResultPlayerState state)
    {
        switch (state)
        {
            case ResultPlayerState.Alive:
                return aliveColor;
            case ResultPlayerState.Dead:
                return deadColor;
            case ResultPlayerState.Empty:
                return emptyColor;
            default:
                return Color.white;
        }
    }

    private void SetPortrait(Sprite portrait, bool visible)
    {
        if (playerPortraitImage == null)
        {
            return;
        }

        playerPortraitImage.sprite = portrait;
        playerPortraitImage.enabled = visible && portrait != null;
    }

    private static void SetText(TextMeshProUGUI targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }
}
