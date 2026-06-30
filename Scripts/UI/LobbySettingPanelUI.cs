using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbySettingPanelUI : MonoBehaviour
{
    [Header("Master Volume")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumePercentText;

    [Header("SFX Volume")]
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI sfxVolumePercentText;

    [Header("Buttons")]
    [SerializeField] private Button closeLobbySettingButton;
    [SerializeField] private Button escButton;
    [SerializeField] private Button applyButton;

    [SerializeField] private UIAudioVolumeController audioVolumeController; // 사운드 관련 추가

    [Header("Outside Click")]
    [SerializeField] private RectTransform popupContentRoot;
    [SerializeField] private bool closeOnOutsideClick = true;

    private float openedBgmVolume = UIAudioSettings.DefaultVolume;
    private float openedSfxVolume = UIAudioSettings.DefaultVolume;
    private int openedFrame;

    private void Awake()
    {
        AutoBind();
        PrepareRaycastTargets();
        InitializeDefaultVolume(masterVolumeSlider);
        InitializeDefaultVolume(sfxVolumeSlider);
        LoadSavedValues();
    }

    private void Start()
    {
        UpdateMasterVolumeText(GetSliderValue(masterVolumeSlider));
        UpdateSfxVolumeText(GetSliderValue(sfxVolumeSlider));
    }

    private void OnEnable()
    {
        openedFrame = Time.frameCount;
        AutoBind();
        PrepareRaycastTargets();
        LoadSavedValues();
        UIAudioSettings.PreviewSaved();

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (closeLobbySettingButton != null)
        {
            closeLobbySettingButton.onClick.AddListener(Hide);
        }

        if (escButton != null)
        {
            escButton.onClick.AddListener(Hide);
        }

        if (applyButton != null)
        {
            applyButton.onClick.AddListener(ApplyAndHide);
        }
    }

    private void OnDisable()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        }

        if (closeLobbySettingButton != null)
        {
            closeLobbySettingButton.onClick.RemoveListener(Hide);
        }

        if (escButton != null)
        {
            escButton.onClick.RemoveListener(Hide);
        }

        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(ApplyAndHide);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            return;
        }

        if (closeOnOutsideClick && Time.frameCount > openedFrame && IsPointerDownOutsidePopup())
        {
            HideFromExternalLayer();
        }
    }

    public void Show()
    {
        LoadSavedValues();
        UIAudioSettings.PreviewSaved();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        PlayButtonClick(); // 사운드 관련 추가
        RevertPreviewToOpenedValues();
        gameObject.SetActive(false);
    }

    public void HideFromExternalLayer()
    {
        RevertPreviewToOpenedValues();
        gameObject.SetActive(false);
    }

    public void ApplyAndHide()
    {
        PlayButtonClick(); // 사운드 관련 추가
        ApplySettings();
        gameObject.SetActive(false);
    }

    // 전체 음량은 개인 로컬 설정으로 처리한다.
    // 다른 플레이어에게 동기화할 값이 아니므로 Fusion 상태와 분리한다.
    public void OnMasterVolumeChanged(float value)
    {
        float normalizedValue = NormalizeSliderValue(masterVolumeSlider, value);
        UpdateMasterVolumeText(normalizedValue);
        UIAudioSettings.Preview(normalizedValue, GetSliderValue(sfxVolumeSlider));
    }

    // 효과음 음량도 개인 로컬 설정으로 처리한다.
    // 실제 AudioMixer 연결은 사운드 시스템이 준비된 뒤 연결한다.
    public void OnSfxVolumeChanged(float value)
    {
        float normalizedValue = NormalizeSliderValue(sfxVolumeSlider, value);
        UpdateSfxVolumeText(normalizedValue);
        UIAudioSettings.Preview(GetSliderValue(masterVolumeSlider), normalizedValue);
    }

    public void ApplySettings()
    {
        float bgmVolume = GetSliderValue(masterVolumeSlider);
        float sfxVolume = GetSliderValue(sfxVolumeSlider);

        UIAudioSettings.Save(bgmVolume, sfxVolume);
        openedBgmVolume = bgmVolume;
        openedSfxVolume = sfxVolume;
        UpdateMasterVolumeText(bgmVolume);
        UpdateSfxVolumeText(sfxVolume);
    }

    private void UpdateMasterVolumeText(float value)
    {
        if (masterVolumePercentText != null)
        {
            masterVolumePercentText.text = ToPercentText(value);
        }
    }

    private void UpdateSfxVolumeText(float value)
    {
        if (sfxVolumePercentText != null)
        {
            sfxVolumePercentText.text = ToPercentText(value);
        }
    }

    private static float GetSliderValue(Slider slider)
    {
        if (slider == null)
        {
            return 0f;
        }

        return NormalizeSliderValue(slider, slider.value);
    }

    private static float NormalizeSliderValue(Slider slider, float rawValue)
    {
        float value = Mathf.Clamp01(rawValue);
        return slider != null && slider.direction == Slider.Direction.RightToLeft ? 1f - value : value;
    }

    private static void InitializeDefaultVolume(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        // 로비 설정창은 처음 열 때 배경음/효과음을 100% 기준으로 보여준다.
        // 실제 저장값 연동이 붙으면 이 기본값 대신 저장된 로컬 값을 넣으면 된다.
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.direction = Slider.Direction.RightToLeft;
        slider.SetValueWithoutNotify(UIAudioSettings.DefaultVolume);
    }

    private static void SetSliderValueWithoutNotify(Slider slider, float value)
    {
        if (slider != null)
        {
            float clampedValue = Mathf.Clamp01(value);
            if (slider.direction == Slider.Direction.RightToLeft)
            {
                clampedValue = 1f - clampedValue;
            }

            slider.SetValueWithoutNotify(clampedValue);
        }
    }

    private static string ToPercentText(float value)
    {
        int percent = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f);
        return $"{percent}%";
    }

    private void LoadSavedValues()
    {
        openedBgmVolume = UIAudioSettings.BgmVolume;
        openedSfxVolume = UIAudioSettings.SfxVolume;
        SetSliderValueWithoutNotify(masterVolumeSlider, openedBgmVolume);
        SetSliderValueWithoutNotify(sfxVolumeSlider, openedSfxVolume);
        UpdateMasterVolumeText(openedBgmVolume);
        UpdateSfxVolumeText(openedSfxVolume);
    }

    private void RevertPreviewToOpenedValues()
    {
        SetSliderValueWithoutNotify(masterVolumeSlider, openedBgmVolume);
        SetSliderValueWithoutNotify(sfxVolumeSlider, openedSfxVolume);
        UpdateMasterVolumeText(openedBgmVolume);
        UpdateSfxVolumeText(openedSfxVolume);
        UIAudioSettings.Preview(openedBgmVolume, openedSfxVolume);
    }

    private void AutoBind()
    {
        audioVolumeController = audioVolumeController != null ? audioVolumeController : FindFirstObjectByType<UIAudioVolumeController>(); // 사운드 관련 추가
        popupContentRoot = popupContentRoot != null ? popupContentRoot : transform as RectTransform;
        masterVolumeSlider = masterVolumeSlider != null ? masterVolumeSlider : FindSlider("MasterVolumeSlider", "BGMGauge", "BgmGauge");
        sfxVolumeSlider = sfxVolumeSlider != null ? sfxVolumeSlider : FindSlider("SfxVolumeSlider", "SFXGauge", "SfxGauge");
        masterVolumePercentText = masterVolumePercentText != null ? masterVolumePercentText : FindText("MasterVolumePercentText", "BGMPercentText", "BgmPercentText", "BGMText");
        sfxVolumePercentText = sfxVolumePercentText != null ? sfxVolumePercentText : FindText("SfxVolumePercentText", "SFXPercentText", "SfxPercentText", "SFXText");
        closeLobbySettingButton = closeLobbySettingButton != null ? closeLobbySettingButton : FindButton("CloseLobbySettingButton", "CloseButton", "ExitBtn");
        escButton = escButton != null ? escButton : FindButton("ESC");
        applyButton = applyButton != null ? applyButton : FindButton("ApplyButton", "ApplyBtn");
    }

    private void PrepareRaycastTargets()
    {
        DisableAllGraphicsRaycastTargets();
        SetRaycastTarget(GetComponent<Graphic>(), true);
        SetRaycastTarget(masterVolumePercentText, false);
        SetRaycastTarget(sfxVolumePercentText, false);
        PrepareButtonHierarchy(closeLobbySettingButton);
        PrepareButtonHierarchy(escButton);
        PrepareButtonHierarchy(applyButton);
        PrepareSliderHierarchy(masterVolumeSlider);
        PrepareSliderHierarchy(sfxVolumeSlider);
    }

    private TextMeshProUGUI FindText(params string[] childNames)
    {
        Transform child = FindChild(childNames);
        return child != null ? child.GetComponentInChildren<TextMeshProUGUI>(true) : null;
    }

    private Button FindButton(params string[] childNames)
    {
        Transform child = FindChild(childNames);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private Slider FindSlider(params string[] childNames)
    {
        Transform child = FindChild(childNames);
        return child != null ? child.GetComponentInChildren<Slider>(true) : null;
    }

    private Transform FindChild(params string[] childNames)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            for (int nameIndex = 0; nameIndex < childNames.Length; nameIndex++)
            {
                if (children[i].name == childNames[nameIndex])
                {
                    return children[i];
                }
            }
        }

        return null;
    }

    private bool IsPointerDownOutsidePopup()
    {
        if (popupContentRoot == null)
        {
            return false;
        }

        if (Input.GetMouseButtonDown(0) && IsScreenPointOutsidePopup(Input.mousePosition))
        {
            return true;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && IsScreenPointOutsidePopup(touch.position))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsScreenPointOutsidePopup(Vector2 screenPoint)
    {
        Camera eventCamera = GetEventCamera();
        return !RectTransformUtility.RectangleContainsScreenPoint(popupContentRoot, screenPoint, eventCamera);
    }

    private Camera GetEventCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private static void SetRaycastTarget(Graphic graphic, bool isRaycastTarget)
    {
        if (graphic != null)
        {
            graphic.raycastTarget = isRaycastTarget;
        }
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

    private void DisableAllGraphicsRaycastTargets()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }

    private static void PrepareSliderHierarchy(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        Graphic[] graphics = slider.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = true;
            }
        }

        Graphic targetGraphic = slider.targetGraphic;
        if (targetGraphic != null)
        {
            targetGraphic.raycastTarget = true;
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
