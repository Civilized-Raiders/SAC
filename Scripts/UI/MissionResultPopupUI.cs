using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionResultPopupUI : MonoBehaviour
{
    [Serializable]
    public struct PlayerResultViewData
    {
        public bool hasPlayer;
        public string playerName;
        public Sprite portrait;
        public bool isAlive;
        public string survivalTimeText;
        public int collectedItemCount;
        public int specialArtifactCount;
    }

    [Header("Panels")]
    [SerializeField] private GameObject missionSuccessPanel;
    [SerializeField] private GameObject missionFailPanel;
    [SerializeField] private Button successPanelClickButton;
    [SerializeField] private Button failPanelClickButton;

    [Header("Success Values")]
    [SerializeField] private TextMeshProUGUI successTargetMoneyText;
    [SerializeField] private TextMeshProUGUI successCollectedMoneyText;
    [SerializeField] private TextMeshProUGUI successExtraMoneyText;
    [Tooltip("게임 규칙에서 확정된 이번 판 총 소비 금액을 표시한다. UI가 직접 계산하지 않는다.")]
    [SerializeField] private TextMeshProUGUI successExpenseText;
    [SerializeField] private TextMeshProUGUI successCountdownText;
    [SerializeField] private Button successConfirmButton;
    [SerializeField] private ResultPlayerSlotUI[] successPlayerSlots;

    [Header("Fail Values")]
    [SerializeField] private TextMeshProUGUI failTargetMoneyText;
    [SerializeField] private TextMeshProUGUI failCollectedMoneyText;
    [SerializeField] private TextMeshProUGUI failMissingMoneyText;
    [Tooltip("게임 규칙에서 확정된 이번 판 총 소비 금액을 표시한다. UI가 직접 계산하지 않는다.")]
    [SerializeField] private TextMeshProUGUI failExpenseText;
    [SerializeField] private TextMeshProUGUI failCountdownText;
    [SerializeField] private Button failConfirmButton;
    [SerializeField] private ResultPlayerSlotUI[] failPlayerSlots;

    [Header("Countdown")]
    [SerializeField] private float autoConfirmSeconds = 15f;
    [SerializeField] private string successCountdownFormat = "{0}초 후 다음 화면으로 이동합니다.";
    [SerializeField] private string failCountdownFormat = "{0}초 후 대기 화면으로 이동합니다.";

    public event Action<bool> ConfirmRequested;

    private Coroutine countdownRoutine;
    private bool isShowingSuccess;
    private bool confirmRequested;

    private void Awake()
    {
        AutoBindPlayerSlotsIfNeeded();
        Hide();
    }

    private void OnEnable()
    {
        Hide();
        AddButtonListener(successPanelClickButton, OnClickConfirmButton);
        AddButtonListener(failPanelClickButton, OnClickConfirmButton);
        AddButtonListener(successConfirmButton, OnClickConfirmButton);
        AddButtonListener(failConfirmButton, OnClickConfirmButton);
    }

    private void OnDisable()
    {
        RemoveButtonListener(successPanelClickButton, OnClickConfirmButton);
        RemoveButtonListener(failPanelClickButton, OnClickConfirmButton);
        RemoveButtonListener(successConfirmButton, OnClickConfirmButton);
        RemoveButtonListener(failConfirmButton, OnClickConfirmButton);
        StopCountdown();
    }

    public void ShowSuccess(int targetMoney, int collectedMoney, PlayerResultViewData[] playerResults)
    {
        ShowSuccess(targetMoney, collectedMoney, 0, playerResults);
    }

    public void ShowSuccess(int targetMoney, int collectedMoney, int expenseMoney, PlayerResultViewData[] playerResults)
    {
        // 결과창 UI는 게임 시스템에서 확정된 성공/실패 결과를 화면에 표시만 담당한다.
        // 다음 스테이지 이동, 로비 복귀, 보상/손실 확정은 UI가 직접 처리하지 않는다.
        isShowingSuccess = true;
        confirmRequested = false;

        SetObjectActive(missionSuccessPanel, true);
        SetObjectActive(missionFailPanel, false);

        int safeTargetMoney = Mathf.Max(0, targetMoney);
        int safeCollectedMoney = Mathf.Max(0, collectedMoney);
        int safeExpenseMoney = Mathf.Max(0, expenseMoney);
        int extraMoney = Mathf.Max(0, safeCollectedMoney - safeTargetMoney);

        SetText(successTargetMoneyText, FormatMoney(safeTargetMoney));
        SetText(successCollectedMoneyText, FormatMoney(safeCollectedMoney));
        SetText(successExtraMoneyText, $"+{FormatMoney(extraMoney)}");
        SetText(successExpenseText, FormatMoney(safeExpenseMoney));
        SetPlayerSlots(successPlayerSlots, playerResults);
        StartCountdown(successCountdownText, successCountdownFormat);
    }

    public void ShowFail(int targetMoney, PlayerResultViewData[] playerResults)
    {
        ShowFail(targetMoney, 0, playerResults);
    }

    public void ShowFail(int targetMoney, int collectedMoney, PlayerResultViewData[] playerResults)
    {
        ShowFail(targetMoney, collectedMoney, 0, playerResults);
    }

    public void ShowFail(int targetMoney, int collectedMoney, int expenseMoney, PlayerResultViewData[] playerResults)
    {
        isShowingSuccess = false;
        confirmRequested = false;

        SetObjectActive(missionSuccessPanel, false);
        SetObjectActive(missionFailPanel, true);

        int safeTargetMoney = Mathf.Max(0, targetMoney);
        int safeCollectedMoney = Mathf.Max(0, collectedMoney);
        int safeExpenseMoney = Mathf.Max(0, expenseMoney);
        int missingMoney = Mathf.Max(0, safeTargetMoney - safeCollectedMoney);

        SetText(failTargetMoneyText, FormatMoney(safeTargetMoney));
        SetText(failCollectedMoneyText, FormatMoney(safeCollectedMoney));
        SetText(failMissingMoneyText, $"-{FormatMoney(missingMoney)}");
        SetText(failExpenseText, FormatMoney(safeExpenseMoney));
        SetPlayerSlots(failPlayerSlots, playerResults);
        StartCountdown(failCountdownText, failCountdownFormat);
    }

    public void Hide()
    {
        StopCountdown();
        confirmRequested = false;
        SetObjectActive(missionSuccessPanel, false);
        SetObjectActive(missionFailPanel, false);
    }

    private void SetPlayerSlots(ResultPlayerSlotUI[] slots, PlayerResultViewData[] playerResults)
    {
        if (slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            ResultPlayerSlotUI slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            if (playerResults == null || i >= playerResults.Length || !playerResults[i].hasPlayer)
            {
                slot.SetEmpty();
                continue;
            }

            PlayerResultViewData result = playerResults[i];
            ResultPlayerSlotUI.ResultPlayerState state = result.isAlive
                ? ResultPlayerSlotUI.ResultPlayerState.Alive
                : ResultPlayerSlotUI.ResultPlayerState.Dead;

            slot.SetPlayerResult(
                result.playerName,
                result.portrait,
                state,
                result.survivalTimeText,
                result.collectedItemCount,
                result.specialArtifactCount);
        }
    }

    private void AutoBindPlayerSlotsIfNeeded()
    {
        if (NeedsAutoBind(successPlayerSlots) && missionSuccessPanel != null)
        {
            successPlayerSlots = missionSuccessPanel
                .GetComponentsInChildren<ResultPlayerSlotUI>(true)
                .OrderBy(slot => slot.name, StringComparer.Ordinal)
                .ToArray();
        }

        if (NeedsAutoBind(failPlayerSlots) && missionFailPanel != null)
        {
            failPlayerSlots = missionFailPanel
                .GetComponentsInChildren<ResultPlayerSlotUI>(true)
                .OrderBy(slot => slot.name, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private static bool NeedsAutoBind(ResultPlayerSlotUI[] slots)
    {
        if (slots == null || slots.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                return false;
            }
        }

        return true;
    }

    private void StartCountdown(TextMeshProUGUI targetText, string format)
    {
        StopCountdown();
        countdownRoutine = StartCoroutine(CountdownRoutine(targetText, format));
    }

    private IEnumerator CountdownRoutine(TextMeshProUGUI targetText, string format)
    {
        float remainSeconds = Mathf.Max(0f, autoConfirmSeconds);

        while (remainSeconds > 0f)
        {
            int displaySeconds = Mathf.CeilToInt(remainSeconds);
            SetText(targetText, string.Format(format, displaySeconds));
            remainSeconds -= Time.unscaledDeltaTime;
            yield return null;
        }

        SetText(targetText, string.Format(format, 0));
        RequestConfirm();
    }

    private void OnClickConfirmButton()
    {
        RequestConfirm();
    }

    private void RequestConfirm()
    {
        if (confirmRequested)
        {
            return;
        }

        confirmRequested = true;
        StopCountdown();
        ConfirmRequested?.Invoke(isShowingSuccess);
    }

    private void StopCountdown()
    {
        if (countdownRoutine == null)
        {
            return;
        }

        StopCoroutine(countdownRoutine);
        countdownRoutine = null;
    }

    private static void SetObjectActive(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    private static void SetText(TextMeshProUGUI targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }

    private static string FormatMoney(int value)
    {
        return Mathf.Max(0, value).ToString("N0");
    }

    private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }

    private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }
}
