using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomPlayerSlotUI : MonoBehaviour
{
    [Header("Visual Options")]
    [SerializeField] private bool hideArmSelectArea = true;
    [SerializeField] private bool hideMicStateIcon = true;
    [SerializeField] private GameObject leftArmArea;
    [SerializeField] private GameObject rightArmArea;

    [Header("Slot Header")]
    [SerializeField] private TextMeshProUGUI slotNumberText;
    [SerializeField] private GameObject hostIcon;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Image micStateIcon;
    [SerializeField] private Sprite micOnSprite;
    [SerializeField] private Sprite micOffSprite;

    [Header("Character Preview")]
    [SerializeField] private Image characterPreviewImage;

    [Header("Ready State")]
    [SerializeField] private TextMeshProUGUI readyStateText;
    [SerializeField] private Image readyStateImage;
    [SerializeField] private Sprite readyWaitingSprite;
    [SerializeField] private Sprite readyCompletedSprite;

    [Header("Arm Select")]
    [SerializeField] private Button leftArmPrevButton;
    [SerializeField] private Image leftArmPreviewImage;
    [SerializeField] private Button leftArmNextButton;
    [SerializeField] private Button rightArmPrevButton;
    [SerializeField] private Image rightArmPreviewImage;
    [SerializeField] private Button rightArmNextButton;

    [Header("Voice Receive")]
    [Tooltip("음성 기능은 현재 기획 보류 상태입니다. 켜면 상대 플레이어 슬롯에서 듣기 볼륨 UI만 표시합니다.")]
    [SerializeField] private bool showVoiceReceiveArea;
    [SerializeField] private GameObject voiceReceiveArea;
    [SerializeField] private Button voiceReceiveMuteButton;
    [SerializeField] private Image voiceReceiveMuteIcon;
    [SerializeField] private Sprite voiceReceiveOnSprite;
    [SerializeField] private Sprite voiceReceiveMutedSprite;
    [SerializeField] private Slider voiceReceiveVolumeSlider;
    [SerializeField] private TextMeshProUGUI voiceReceiveVolumePercentText;

    [Header("Empty Slot")]
    [SerializeField] private GameObject occupiedContentRoot;
    [SerializeField] private GameObject emptySlotRoot;
    [SerializeField] private TextMeshProUGUI emptySlotText;

    public event Action<RoomPlayerSlotUI> LeftArmPrevRequested;
    public event Action<RoomPlayerSlotUI> LeftArmNextRequested;
    public event Action<RoomPlayerSlotUI> RightArmPrevRequested;
    public event Action<RoomPlayerSlotUI> RightArmNextRequested;
    public event Action<RoomPlayerSlotUI, bool> VoiceReceiveMuteChanged;
    public event Action<RoomPlayerSlotUI, float> VoiceReceiveVolumeChanged;

    public int SlotNumber { get; private set; }
    public bool HasPlayer { get; private set; }
    public bool IsLocalPlayerSlot { get; private set; }

    private bool isVoiceReceiveMuted;

    private void Awake()
    {
        AutoBind();
        ApplyOptionalVisualVisibility();
        PrepareRaycastTargets();
    }

    private void OnEnable()
    {
        AutoBind();
        ApplyOptionalVisualVisibility();
        PrepareRaycastTargets();
        AddButtonListeners();

        if (voiceReceiveVolumeSlider != null)
        {
            voiceReceiveVolumeSlider.onValueChanged.AddListener(OnVoiceReceiveVolumeChanged);
            UpdateVoiceReceiveVolumeText(voiceReceiveVolumeSlider.value);
        }
    }

    private void OnDisable()
    {
        RemoveButtonListeners();

        if (voiceReceiveVolumeSlider != null)
        {
            voiceReceiveVolumeSlider.onValueChanged.RemoveListener(OnVoiceReceiveVolumeChanged);
        }
    }

    // 슬롯 번호는 입장 순서 또는 인게임에서 사용할 플레이어 번호로 표시한다.
    // 실제 순서는 나중에 Fusion에서 확정된 플레이어 목록 기준으로 적용한다.
    public void SetSlotNumber(int slotNumber)
    {
        SlotNumber = slotNumber;

        if (slotNumberText != null)
        {
            slotNumberText.text = slotNumber.ToString();
        }
    }

    // 플레이어가 들어온 슬롯에 기본 표시 정보를 적용한다.
    // UI는 Host 여부나 플레이어 이름을 직접 판단하지 않고 외부에서 받은 값을 표시만 한다.
    public void SetPlayerInfo(string playerName, bool isHost, bool isLocalPlayerSlot)
    {
        HasPlayer = true;
        IsLocalPlayerSlot = isLocalPlayerSlot;

        SetObjectActive(occupiedContentRoot, true);
        SetObjectActive(emptySlotRoot, false);

        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        SetObjectActive(hostIcon, isHost && HasVisibleHostIcon(hostIcon));
        SetLocalPlayerControls(isLocalPlayerSlot);
    }

    // 빈 슬롯은 아직 입장하지 않은 플레이어 자리로 표시한다.
    // 나중에 Fusion 참가자 목록이 갱신되면 다시 SetPlayerInfo로 채운다.
    public void SetEmptySlot(int slotNumber)
    {
        SetSlotNumber(slotNumber);
        HasPlayer = false;
        IsLocalPlayerSlot = false;

        SetObjectActive(occupiedContentRoot, false);
        SetObjectActive(emptySlotRoot, true);
        SetObjectActive(hostIcon, false);

        if (emptySlotText != null)
        {
            emptySlotText.text = "빈 슬롯";
        }

        ClearPlayerDisplay();
        SetLocalPlayerControls(false);
        SetVoiceReceiveVisible(false);
    }

    // 마이크 아이콘은 해당 플레이어의 마이크 On/Off 상태를 보여준다.
    // 이 값은 내 로컬 설정이 아니라 해당 플레이어 상태를 받아 표시하는 값이다.
    public void SetMicState(bool isMicOn)
    {
        if (micStateIcon == null)
        {
            return;
        }

        if (hideMicStateIcon)
        {
            micStateIcon.enabled = false;
            return;
        }

        micStateIcon.sprite = isMicOn ? micOnSprite : micOffSprite;
        micStateIcon.enabled = micStateIcon.sprite != null;
    }

    // 캐릭터 이미지는 나중에 Title 또는 커스텀 화면에서 정한 외형/색상 결과를 표시한다.
    public void SetCharacterPreview(Sprite characterSprite, Color characterColor)
    {
        if (characterPreviewImage == null)
        {
            return;
        }

        characterPreviewImage.sprite = characterSprite;
        characterPreviewImage.color = characterColor;
        characterPreviewImage.enabled = characterSprite != null;
    }

    // Ready 상태는 네트워크에서 확정된 값을 표시한다.
    // 버튼을 눌렀다고 UI가 먼저 확정하지 않고, 나중에 Fusion 결과를 받아 갱신한다.
    public void SetReadyState(bool isReady)
    {
        if (readyStateText != null)
        {
            readyStateText.text = isReady ? "승인 완료" : "승인 대기";
        }

        if (readyStateImage != null)
        {
            Sprite stateSprite = isReady ? readyCompletedSprite : readyWaitingSprite;
            readyStateImage.sprite = stateSprite;
            readyStateImage.enabled = stateSprite != null;
        }
    }

    // 팔 이미지는 현재 확정된 왼팔/오른팔 장비를 표시한다.
    // 팔 이름과 설명은 추후 이미지 안에 포함될 수 있으므로 텍스트에 의존하지 않는다.
    public void SetArmPreview(Sprite leftArmSprite, Sprite rightArmSprite)
    {
        SetImage(leftArmPreviewImage, leftArmSprite);
        SetImage(rightArmPreviewImage, rightArmSprite);
    }

    // 팔 선택 버튼은 내 슬롯에서만 활성화한다.
    // 다른 플레이어의 팔은 보기 전용이며, 내가 변경할 수 없다.
    public void SetLocalPlayerControls(bool isLocalPlayerSlot)
    {
        IsLocalPlayerSlot = isLocalPlayerSlot;

        SetButtonInteractable(leftArmPrevButton, isLocalPlayerSlot && HasPlayer);
        SetButtonInteractable(leftArmNextButton, isLocalPlayerSlot && HasPlayer);
        SetButtonInteractable(rightArmPrevButton, isLocalPlayerSlot && HasPlayer);
        SetButtonInteractable(rightArmNextButton, isLocalPlayerSlot && HasPlayer);

        // 음성 기능은 현재 보류 중이므로 기본값에서는 UI를 숨긴다.
        // 추후 음성 기획이 확정되면 Inspector에서 showVoiceReceiveArea를 켜서 표시만 재사용한다.
        SetVoiceReceiveVisible(showVoiceReceiveArea && HasPlayer && !isLocalPlayerSlot);
    }

    // VoiceReceiveArea는 상대방 마이크 자체를 조절하는 기능이 아니다.
    // 내가 이 플레이어의 목소리를 듣는 로컬 볼륨만 조절한다.
    public void SetVoiceReceiveVolume(float value)
    {
        float safeValue = Mathf.Clamp01(value);

        if (voiceReceiveVolumeSlider != null)
        {
            voiceReceiveVolumeSlider.SetValueWithoutNotify(safeValue);
        }

        UpdateVoiceReceiveVolumeText(safeValue);
    }

    // 특정 플레이어의 음성을 내가 들을지 말지 정한다.
    // 이 값은 내 로컬 수신 설정이므로 다른 플레이어에게 동기화하지 않는다.
    public void SetVoiceReceiveMuted(bool isMuted)
    {
        isVoiceReceiveMuted = isMuted;

        if (voiceReceiveMuteIcon != null)
        {
            voiceReceiveMuteIcon.sprite = isMuted ? voiceReceiveMutedSprite : voiceReceiveOnSprite;
            voiceReceiveMuteIcon.enabled = voiceReceiveMuteIcon.sprite != null;
        }
    }

    public void OnClickLeftArmPrevButton()
    {
        if (IsLocalPlayerSlot && HasPlayer)
        {
            LeftArmPrevRequested?.Invoke(this);
        }
    }

    public void OnClickLeftArmNextButton()
    {
        if (IsLocalPlayerSlot && HasPlayer)
        {
            LeftArmNextRequested?.Invoke(this);
        }
    }

    public void OnClickRightArmPrevButton()
    {
        if (IsLocalPlayerSlot && HasPlayer)
        {
            RightArmPrevRequested?.Invoke(this);
        }
    }

    public void OnClickRightArmNextButton()
    {
        if (IsLocalPlayerSlot && HasPlayer)
        {
            RightArmNextRequested?.Invoke(this);
        }
    }

    public void OnClickVoiceReceiveMuteButton()
    {
        if (!HasPlayer || IsLocalPlayerSlot)
        {
            return;
        }

        SetVoiceReceiveMuted(!isVoiceReceiveMuted);
        VoiceReceiveMuteChanged?.Invoke(this, isVoiceReceiveMuted);
    }

    private void OnVoiceReceiveVolumeChanged(float value)
    {
        float safeValue = Mathf.Clamp01(value);
        UpdateVoiceReceiveVolumeText(safeValue);
        VoiceReceiveVolumeChanged?.Invoke(this, safeValue);
    }

    private void AddButtonListeners()
    {
        AddListener(leftArmPrevButton, OnClickLeftArmPrevButton);
        AddListener(leftArmNextButton, OnClickLeftArmNextButton);
        AddListener(rightArmPrevButton, OnClickRightArmPrevButton);
        AddListener(rightArmNextButton, OnClickRightArmNextButton);
        AddListener(voiceReceiveMuteButton, OnClickVoiceReceiveMuteButton);
    }

    private void RemoveButtonListeners()
    {
        RemoveListener(leftArmPrevButton, OnClickLeftArmPrevButton);
        RemoveListener(leftArmNextButton, OnClickLeftArmNextButton);
        RemoveListener(rightArmPrevButton, OnClickRightArmPrevButton);
        RemoveListener(rightArmNextButton, OnClickRightArmNextButton);
        RemoveListener(voiceReceiveMuteButton, OnClickVoiceReceiveMuteButton);
    }

    private void SetVoiceReceiveVisible(bool isVisible)
    {
        SetObjectActive(voiceReceiveArea, isVisible);
    }

    private void UpdateVoiceReceiveVolumeText(float value)
    {
        if (voiceReceiveVolumePercentText != null)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f);
            voiceReceiveVolumePercentText.text = $"{percent}%";
        }
    }

    private void ClearPlayerDisplay()
    {
        // 빈 슬롯은 플레이어가 아직 들어오지 않은 자리이므로 임시 흰 이미지나 준비 문구를 남기지 않는다.
        // 실제 입장 여부는 Fusion/Mock 데이터가 정하고, UI는 빈 상태를 투명하게 표시만 한다.
        if (playerNameText != null)
        {
            playerNameText.text = string.Empty;
        }

        if (readyStateText != null)
        {
            readyStateText.text = string.Empty;
        }

        SetImage(readyStateImage, null);

        if (micStateIcon != null)
        {
            micStateIcon.enabled = false;
        }

        SetImage(characterPreviewImage, null);
        SetImage(leftArmPreviewImage, null);
        SetImage(rightArmPreviewImage, null);
    }

    private void AutoBind()
    {
        slotNumberText = slotNumberText != null ? slotNumberText : FindText("SlotNumberText");
        playerNameText = playerNameText != null ? playerNameText : FindText("PlayerNameText");
        readyStateText = readyStateText != null ? readyStateText : FindText("ReadyStateText");
        voiceReceiveVolumePercentText = voiceReceiveVolumePercentText != null ? voiceReceiveVolumePercentText : FindText("VoiceReceiveVolumePercentText");
        emptySlotText = emptySlotText != null ? emptySlotText : FindText("EmptySlotText");

        if (hostIcon == null || hostIcon.name != "HostIcon")
        {
            hostIcon = FindChildObject("HostIcon");
        }
        leftArmArea = leftArmArea != null ? leftArmArea : FindChildObject("LeftArmArea");
        rightArmArea = rightArmArea != null ? rightArmArea : FindChildObject("RightArmArea");
        voiceReceiveArea = voiceReceiveArea != null ? voiceReceiveArea : FindChildObject("VoiceReceiveArea");
        occupiedContentRoot = occupiedContentRoot != null ? occupiedContentRoot : FindChildObject("OccupiedContentRoot");
        emptySlotRoot = emptySlotRoot != null ? emptySlotRoot : FindChildObject("EmptySlotRoot");

        micStateIcon = micStateIcon != null ? micStateIcon : FindImage("MicStateIcon");
        characterPreviewImage = characterPreviewImage != null ? characterPreviewImage : FindImage("CharacterPreviewImage");
        readyStateImage = readyStateImage != null ? readyStateImage : FindImage("ReadyStateImage");
        leftArmPreviewImage = leftArmPreviewImage != null ? leftArmPreviewImage : FindImage("LeftArmPreviewImage");
        rightArmPreviewImage = rightArmPreviewImage != null ? rightArmPreviewImage : FindImage("RightArmPreviewImage");
        voiceReceiveMuteIcon = voiceReceiveMuteIcon != null ? voiceReceiveMuteIcon : FindImage("VoiceReceiveMuteIcon");

        leftArmPrevButton = leftArmPrevButton != null ? leftArmPrevButton : FindButton("LeftArmPrevButton");
        leftArmNextButton = leftArmNextButton != null ? leftArmNextButton : FindButton("LeftArmNextButton");
        rightArmPrevButton = rightArmPrevButton != null ? rightArmPrevButton : FindButton("RightArmPrevButton");
        rightArmNextButton = rightArmNextButton != null ? rightArmNextButton : FindButton("RightArmNextButton");
        voiceReceiveMuteButton = voiceReceiveMuteButton != null ? voiceReceiveMuteButton : FindButton("VoiceReceiveMuteButton");

        voiceReceiveVolumeSlider = voiceReceiveVolumeSlider != null ? voiceReceiveVolumeSlider : FindSlider("VoiceReceiveVolumeSlider");
    }

    private void ApplyOptionalVisualVisibility()
    {
        if (hideArmSelectArea)
        {
            SetObjectActive(leftArmArea, false);
            SetObjectActive(rightArmArea, false);
        }

        if (hideMicStateIcon && micStateIcon != null)
        {
            micStateIcon.enabled = false;
        }
    }

    // 슬롯 내부의 표시용 이미지가 우측 설정/색상 UI 클릭을 가로채지 않도록
    // 표시 전용 Graphic은 레이캐스트를 끄고, 실제 버튼만 입력을 받게 유지한다.
    private void PrepareRaycastTargets()
    {
        SetRaycastTarget(slotNumberText, false);
        SetRaycastTarget(playerNameText, false);
        SetRaycastTarget(emptySlotText, false);
        SetRaycastTarget(readyStateText, false);
        SetRaycastTarget(characterPreviewImage, false);
        SetRaycastTarget(readyStateImage, false);
        SetRaycastTarget(micStateIcon, false);
        SetRaycastTarget(leftArmPreviewImage, false);
        SetRaycastTarget(rightArmPreviewImage, false);
        SetRaycastTarget(voiceReceiveMuteIcon, false);
        SetRaycastTarget(voiceReceiveVolumePercentText, false);

        PrepareButtonHierarchy(leftArmPrevButton);
        PrepareButtonHierarchy(leftArmNextButton);
        PrepareButtonHierarchy(rightArmPrevButton);
        PrepareButtonHierarchy(rightArmNextButton);
        PrepareButtonHierarchy(voiceReceiveMuteButton);

        SetRaycastTargetOnObject(hostIcon, false);
    }

    private TextMeshProUGUI FindText(string childName)
    {
        Transform child = FindChild(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private Image FindImage(string childName)
    {
        Transform child = FindChild(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private Button FindButton(string childName)
    {
        Transform child = FindChild(childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private Slider FindSlider(string childName)
    {
        Transform child = FindChild(childName);
        return child != null ? child.GetComponent<Slider>() : null;
    }

    private GameObject FindChildObject(string childName)
    {
        Transform child = FindChild(childName);
        return child != null ? child.gameObject : null;
    }

    private Transform FindChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private static void SetImage(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static void SetButtonInteractable(Button button, bool isInteractable)
    {
        if (button != null)
        {
            button.interactable = isInteractable;
        }
    }

    private static void SetObjectActive(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
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

    private static void SetRaycastTarget(Graphic graphic, bool isRaycastTarget)
    {
        if (graphic != null)
        {
            graphic.raycastTarget = isRaycastTarget;
        }
    }

    private static void SetRaycastTargetOnObject(GameObject target, bool isRaycastTarget)
    {
        if (target == null)
        {
            return;
        }

        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = isRaycastTarget;
        }
    }

    private static bool HasVisibleHostIcon(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        Image iconImage = target.GetComponent<Image>();
        if (iconImage == null)
        {
            return true;
        }

        return iconImage.sprite != null || iconImage.overrideSprite != null;
    }

    private static void PrepareButtonHierarchy(Button button)
    {
        if (button == null)
        {
            return;
        }

        Graphic targetGraphic = button.targetGraphic;
        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            graphic.raycastTarget = graphic == targetGraphic;
        }
    }
}
