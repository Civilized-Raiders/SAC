// Modified: Leave에서 직접 SceneManager.LoadScene 호출 제거(좀비 세션 방지),
//           useTemporaryReadyToggle 기본값 false, Host 아닐 때 HostStartButton 숨김.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomBottomActionUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private Button hostStartButton;
    [SerializeField] private bool hideReadyButtonText = true;

    [SerializeField] private UIAudioVolumeController audioVolumeController; // 사운드 관련 추가

    [Header("Temporary Test")]
    [SerializeField] private RoomPlayerSlotUI localPlayerSlot;
    [SerializeField] private RoomPlayerSlotUI[] playerSlots;
    // ▼ 변경: 네트워크에서 확정된 Ready 만 표시하기 위해 기본값을 false 로.
    [SerializeField] private bool useTemporaryReadyToggle = false;
    [SerializeField] private bool isLocalPlayerHost = true;
    [SerializeField] private int minStartPlayerCount = 1;

    private bool externalStartAllowed;
    private bool useExternalStartGate;

    public event Action LeaveRoomRequested;
    public event Action<bool> ReadyStateChangeRequested;
    public event Action HostStartRequested;

    private bool isLocalPlayerReady;

    private void Start()
    {
        audioVolumeController = audioVolumeController != null ? audioVolumeController : FindFirstObjectByType<UIAudioVolumeController>(); // 사운드 관련 추가
        RefreshReadyButtonTextVisibility();
        SetReadyButtonText(false);
        RefreshHostStartButton();
    }

    private void OnEnable()
    {
        audioVolumeController = audioVolumeController != null ? audioVolumeController : FindFirstObjectByType<UIAudioVolumeController>(); // 사운드 관련 추가
        AddButtonListeners();
    }

    private void OnDisable()
    {
        RemoveButtonListeners();
    }

    public void OnClickLeaveRoomButton()
    {
        PlayButtonClick(); // 사운드 관련 추가
        LeaveRoomRequested?.Invoke();
    }

    public void OnClickReadyButton()
    {
        PlayButtonClick(); // 사운드 관련 추가
        bool nextReadyState = !isLocalPlayerReady;
        ReadyStateChangeRequested?.Invoke(nextReadyState);

        if (useTemporaryReadyToggle)
        {
            ApplyLocalReadyState(nextReadyState);
        }
    }

    public void OnClickHostStartButton()
    {
        if (!CanHostStart())
        {
            return;
        }

        PlayButtonClick(); // 사운드 관련 추가

        readyButton.interactable = false;
        hostStartButton.interactable = false;

        HostStartRequested?.Invoke();
    }

    public void ApplyLocalReadyState(bool isReady)
    {
        isLocalPlayerReady = isReady;
        SetReadyButtonText(isReady);

        if (localPlayerSlot != null)
        {
            localPlayerSlot.SetReadyState(isReady);
        }

        RefreshHostStartButton();
    }

    public void SetHostState(bool isHost)
    {
        isLocalPlayerHost = isHost;
        if (hostStartButton != null)
        {
            hostStartButton.gameObject.SetActive(isHost);
        }
        RefreshHostStartButton();
    }

    public void SetStartAvailability(bool canStart)
    {
        useExternalStartGate = true;
        externalStartAllowed = canStart;
        RefreshHostStartButton();
    }


    public void RefreshHostStartButton()
    {
        if (hostStartButton != null)
        {
            hostStartButton.interactable = CanHostStart();
        }
    }

    private bool CanHostStart()
    {
        if (!isLocalPlayerHost)
        {
            return false;
        }

        if (useExternalStartGate)
        {
            return externalStartAllowed;
        }

        if (playerSlots == null || playerSlots.Length == 0)
        {
            return isLocalPlayerReady && minStartPlayerCount <= 1;
        }

        int activePlayerCount = 0;
        for (int i = 0; i < playerSlots.Length; i++)
        {
            RoomPlayerSlotUI slot = playerSlots[i];
            if (slot != null && slot.HasPlayer)
            {
                activePlayerCount++;
            }
        }

        if (activePlayerCount < minStartPlayerCount)
        {
            return false;
        }

        return isLocalPlayerReady;
    }

    private void SetReadyButtonText(bool isReady)
    {
        if (readyButtonText != null)
        {
            RefreshReadyButtonTextVisibility();
            readyButtonText.text = isReady ? "준비 취소" : "준비";
        }
    }

    public void SetInteractable(bool interactable)
    {
        if (readyButton != null) readyButton.interactable = interactable;
        if (hostStartButton != null) hostStartButton.interactable = interactable;
    }

    private void RefreshReadyButtonTextVisibility()
    {
        if (readyButtonText != null)
        {
            readyButtonText.gameObject.SetActive(!hideReadyButtonText);
        }
    }

    private void AddButtonListeners()
    {
        AddListener(leaveRoomButton, OnClickLeaveRoomButton);
        AddListener(readyButton, OnClickReadyButton);
        AddListener(hostStartButton, OnClickHostStartButton);
    }

    private void RemoveButtonListeners()
    {
        RemoveListener(leaveRoomButton, OnClickLeaveRoomButton);
        RemoveListener(readyButton, OnClickReadyButton);
        RemoveListener(hostStartButton, OnClickHostStartButton);
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }

    // 사운드 관련 추가
    private void PlayButtonClick()
    {
        if (audioVolumeController != null)
        {
            audioVolumeController.PlayButtonClick();
        }
    }
}
