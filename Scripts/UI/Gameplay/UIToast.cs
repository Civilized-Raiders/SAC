using TMPro;
using UnityEngine;
using UnityEngine.UI;


/*
 * [UI Toast] 5 / 29
 * 화면 하단에 잠시 나타나는 간단한 메시지 UI입니다.
 * UIEventManager의 OnShowToast 이벤트를 구독하여 메시지를 표시합니다.
 * [사용 예시]
 * UIEventManager.TriggerShowToast("인벤토리 가득 참");
 */

public class UIToast : MonoBehaviour
{
    [Header("Toast UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Settings")]
    [SerializeField] private float duration = 2f;

    private float timer;

    private void Awake()
    {
        ConfigureLayout();

        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void OnEnable()
    {
        UIEventManager.OnShowToast -= ShowToast;
        UIEventManager.OnShowToast += ShowToast;
    }

    private void OnDisable()
    {
        UIEventManager.OnShowToast -= ShowToast;
    }

    private void Update()
    {
        if (root == null || !root.activeSelf) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            root.SetActive(false);
        }
    }

    private void ShowToast(string message)
    {
        if (root == null || messageText == null) return;

        ConfigureLayout();
        root.SetActive(true);
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.textWrappingMode = TextWrappingModes.Normal;
        messageText.text = message;

        timer = duration;
    }

    private void ConfigureLayout()
    {
        if (root == null)
        {
            return;
        }

        RectTransform rootRect = root.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(
                Mathf.Max(rootRect.sizeDelta.x, 720f),
                Mathf.Max(rootRect.sizeDelta.y, 180f));
        }

        if (messageText != null)
        {
            RectTransform textRect = messageText.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(36f, 24f);
            textRect.offsetMax = new Vector2(-36f, -24f);

            LayoutElement layoutElement = messageText.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = false;
            }
        }
    }
}
