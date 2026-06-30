using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : UIPanel
{
    [Header("Navigation")]
    [SerializeField] private UIFlowController flowController;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button escButton;
    [SerializeField] private Button applyButton;

    [Header("Outside Click")]
    [SerializeField] private RectTransform popupContentRoot;
    [SerializeField] private bool closeOnOutsideClick = true;

    [Header("Audio")]
    [SerializeField] private UIAudioVolumeController audioVolumeController; // 사운드 관련 추가
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumePercentText;
    [SerializeField] private TextMeshProUGUI sfxVolumePercentText;

    private float openedBgmVolume = UIAudioSettings.DefaultVolume;
    private float openedSfxVolume = UIAudioSettings.DefaultVolume;
    private int openedFrame;

    private void Awake()
    {
        AutoBind();
        InitializeSlider(masterVolumeSlider);
        InitializeSlider(sfxVolumeSlider);
        LoadSavedValues();
    }

    private void OnEnable()
    {
        openedFrame = Time.frameCount;
        AutoBind();
        LoadSavedValues();
        BindButtons();
        BindSliders();
        RefreshTexts();
    }

    private void OnDisable()
    {
        UnbindButtons();
        UnbindSliders();
    }

    private void Update()
    {
        if (closeOnOutsideClick && Time.frameCount > openedFrame && IsPointerDownOutsidePopup())
        {
            CloseFromOutsideClick();
        }
    }

    public void OnCloseClicked()
    {
        PlayButtonClick(); // 사운드 관련 추가
        RevertPreviewToOpenedValues();

        if (flowController != null)
        {
            flowController.GoBack();
            return;
        }

        gameObject.SetActive(false);
    }

    public void ApplySettings()
    {
        PlayButtonClick(); // 사운드 관련 추가

        float bgmVolume = GetSliderValue(masterVolumeSlider);
        float sfxVolume = GetSliderValue(sfxVolumeSlider);

        UIAudioSettings.Save(bgmVolume, sfxVolume);
        openedBgmVolume = bgmVolume;
        openedSfxVolume = sfxVolume;
        RefreshTexts();

        if (flowController != null)
        {
            flowController.GoBack();
            return;
        }

        gameObject.SetActive(false);
    }

    private void CloseFromOutsideClick()
    {
        RevertPreviewToOpenedValues();

        if (flowController != null)
        {
            flowController.GoBack();
            return;
        }

        gameObject.SetActive(false);
    }

    private void OnMasterVolumeChanged(float value)
    {
        float normalizedValue = NormalizeSliderValue(masterVolumeSlider, value);
        SetText(masterVolumePercentText, UIAudioSettings.ToPercentText(normalizedValue));
        UIAudioSettings.Preview(normalizedValue, GetSliderValue(sfxVolumeSlider));
    }

    private void OnSfxVolumeChanged(float value)
    {
        float normalizedValue = NormalizeSliderValue(sfxVolumeSlider, value);
        SetText(sfxVolumePercentText, UIAudioSettings.ToPercentText(normalizedValue));
        UIAudioSettings.Preview(GetSliderValue(masterVolumeSlider), normalizedValue);
    }

    private void LoadSavedValues()
    {
        openedBgmVolume = UIAudioSettings.BgmVolume;
        openedSfxVolume = UIAudioSettings.SfxVolume;
        SetSliderValueWithoutNotify(masterVolumeSlider, openedBgmVolume);
        SetSliderValueWithoutNotify(sfxVolumeSlider, openedSfxVolume);
        UIAudioSettings.PreviewSaved();
        RefreshTexts();
    }

    private void RefreshTexts()
    {
        SetText(masterVolumePercentText, UIAudioSettings.ToPercentText(GetSliderValue(masterVolumeSlider)));
        SetText(sfxVolumePercentText, UIAudioSettings.ToPercentText(GetSliderValue(sfxVolumeSlider)));
    }

    private void RevertPreviewToOpenedValues()
    {
        SetSliderValueWithoutNotify(masterVolumeSlider, openedBgmVolume);
        SetSliderValueWithoutNotify(sfxVolumeSlider, openedSfxVolume);
        UIAudioSettings.Preview(openedBgmVolume, openedSfxVolume);
        RefreshTexts();
    }

    private void AutoBind()
    {
        flowController = flowController != null ? flowController : GetComponentInParent<UIFlowController>(true);
        audioVolumeController = audioVolumeController != null ? audioVolumeController : FindFirstObjectByType<UIAudioVolumeController>(); // 사운드 관련 추가
        popupContentRoot = popupContentRoot != null ? popupContentRoot : transform as RectTransform;
        closeButton = closeButton != null ? closeButton : FindButton("CloseButton");
        escButton = escButton != null ? escButton : FindButton("ESC");
        applyButton = applyButton != null ? applyButton : FindButton("ApplyButton");
        masterVolumeSlider = masterVolumeSlider != null ? masterVolumeSlider : FindSlider("MasterVolumeSlider");
        sfxVolumeSlider = sfxVolumeSlider != null ? sfxVolumeSlider : FindSlider("SfxVolumeSlider");
        masterVolumePercentText = masterVolumePercentText != null ? masterVolumePercentText : FindText("MasterVolumePercentText");
        sfxVolumePercentText = sfxVolumePercentText != null ? sfxVolumePercentText : FindText("SfxVolumePercentText");
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

    private void BindButtons()
    {
        AddListener(closeButton, OnCloseClicked);
        AddListener(escButton, OnCloseClicked);
        AddListener(applyButton, ApplySettings);
    }

    private void UnbindButtons()
    {
        RemoveListener(closeButton, OnCloseClicked);
        RemoveListener(escButton, OnCloseClicked);
        RemoveListener(applyButton, ApplySettings);
    }

    private void BindSliders()
    {
        AddSliderListener(masterVolumeSlider, OnMasterVolumeChanged);
        AddSliderListener(sfxVolumeSlider, OnSfxVolumeChanged);
    }

    private void UnbindSliders()
    {
        RemoveSliderListener(masterVolumeSlider, OnMasterVolumeChanged);
        RemoveSliderListener(sfxVolumeSlider, OnSfxVolumeChanged);
    }

    private static void InitializeSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.direction = Slider.Direction.RightToLeft;
    }

    private static float GetSliderValue(Slider slider)
    {
        if (slider == null)
        {
            return UIAudioSettings.DefaultVolume;
        }

        return NormalizeSliderValue(slider, slider.value);
    }

    private static float NormalizeSliderValue(Slider slider, float rawValue)
    {
        float value = Mathf.Clamp01(rawValue);
        return slider != null && slider.direction == Slider.Direction.RightToLeft ? 1f - value : value;
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

    private TextMeshProUGUI FindText(string childName)
    {
        Transform child = UIFlowController.FindChild(transform, childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private Button FindButton(string childName)
    {
        Transform child = UIFlowController.FindChild(transform, childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private Slider FindSlider(string childName)
    {
        Transform child = UIFlowController.FindChild(transform, childName);
        return child != null ? child.GetComponent<Slider>() : null;
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    private static void AddSliderListener(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveListener(action);
        slider.onValueChanged.AddListener(action);
    }

    private static void RemoveSliderListener(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveListener(action);
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
        {
            target.text = value;
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
