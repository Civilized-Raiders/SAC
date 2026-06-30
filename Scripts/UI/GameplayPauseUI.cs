using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayPauseUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject pauseRoot;

    [Header("Volume")]
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI bgmVolumePercentText;
    [SerializeField] private TextMeshProUGUI sfxVolumePercentText;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button quitGameButton;

    public event Action PauseOpened;
    public event Action PauseClosed;
    public event Action QuitGameRequested;

    private bool isOpen;
    private float openedBgmVolume = UIAudioSettings.DefaultVolume;
    private float openedSfxVolume = UIAudioSettings.DefaultVolume;

    private void Awake()
    {
        AutoBindReferences();
        InitializeSlider(bgmVolumeSlider);
        InitializeSlider(sfxVolumeSlider);
        GameplayInputGuard.ResetBlocked();
        CloseWithoutNotify();
    }

    private void OnEnable()
    {
        AddButtonListener(resumeButton, Close);
        AddButtonListener(applyButton, ApplyVolumeSettings);
        AddButtonListener(quitGameButton, RequestQuitGame);
        AddSliderListener(bgmVolumeSlider, OnBgmVolumePreviewChanged);
        AddSliderListener(sfxVolumeSlider, OnSfxVolumePreviewChanged);
    }

    private void OnDisable()
    {
        RemoveButtonListener(resumeButton, Close);
        RemoveButtonListener(applyButton, ApplyVolumeSettings);
        RemoveButtonListener(quitGameButton, RequestQuitGame);
        RemoveSliderListener(bgmVolumeSlider, OnBgmVolumePreviewChanged);
        RemoveSliderListener(sfxVolumeSlider, OnSfxVolumePreviewChanged);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Toggle();
        }

        if (isOpen)
        {
            GameplayInputGuard.ApplyCurrentCursorState();
        }
    }

    public void Open()
    {
        LoadAppliedVolumeSettings();
        SetRootActive(true);
        isOpen = true;
        GameplayInputGuard.SetBlocked(true);
        PauseOpened?.Invoke();
    }

    public void Close()
    {
        RevertPreviewToOpenedValues();
        CloseWithoutNotify();
        GameplayInputGuard.SetBlocked(false);
        PauseClosed?.Invoke();
    }

    public void Toggle()
    {
        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void ApplyVolumeSettings()
    {
        float bgmVolume = GetSliderValue(bgmVolumeSlider);
        float sfxVolume = GetSliderValue(sfxVolumeSlider);

        UIAudioSettings.Save(bgmVolume, sfxVolume);
        openedBgmVolume = bgmVolume;
        openedSfxVolume = sfxVolume;
        UpdateVolumeTexts(bgmVolume, sfxVolume);
    }

    private void RequestQuitGame()
    {
        QuitGameRequested?.Invoke();
    }

    private void LoadAppliedVolumeSettings()
    {
        float bgmVolume = UIAudioSettings.BgmVolume;
        float sfxVolume = UIAudioSettings.SfxVolume;
        openedBgmVolume = bgmVolume;
        openedSfxVolume = sfxVolume;

        SetSliderValueWithoutNotify(bgmVolumeSlider, bgmVolume);
        SetSliderValueWithoutNotify(sfxVolumeSlider, sfxVolume);
        UIAudioSettings.PreviewSaved();
        UpdateVolumeTexts(bgmVolume, sfxVolume);
    }

    private void OnBgmVolumePreviewChanged(float value)
    {
        float normalizedValue = NormalizeSliderValue(bgmVolumeSlider, value);
        SetText(bgmVolumePercentText, UIAudioSettings.ToPercentText(normalizedValue));
        UIAudioSettings.Preview(normalizedValue, GetSliderValue(sfxVolumeSlider));
    }

    private void OnSfxVolumePreviewChanged(float value)
    {
        float normalizedValue = NormalizeSliderValue(sfxVolumeSlider, value);
        SetText(sfxVolumePercentText, UIAudioSettings.ToPercentText(normalizedValue));
        UIAudioSettings.Preview(GetSliderValue(bgmVolumeSlider), normalizedValue);
    }

    private void UpdateVolumeTexts(float bgmVolume, float sfxVolume)
    {
        SetText(bgmVolumePercentText, UIAudioSettings.ToPercentText(bgmVolume));
        SetText(sfxVolumePercentText, UIAudioSettings.ToPercentText(sfxVolume));
    }

    private void CloseWithoutNotify()
    {
        SetRootActive(false);
        isOpen = false;
    }

    private void RevertPreviewToOpenedValues()
    {
        SetSliderValueWithoutNotify(bgmVolumeSlider, openedBgmVolume);
        SetSliderValueWithoutNotify(sfxVolumeSlider, openedSfxVolume);
        UIAudioSettings.Preview(openedBgmVolume, openedSfxVolume);
        UpdateVolumeTexts(openedBgmVolume, openedSfxVolume);
    }

    private void AutoBindReferences()
    {
        if (pauseRoot == null)
        {
            pauseRoot = FindInLoadedScenes("GameplayPauseRoot");
        }

        if (bgmVolumeSlider == null)
        {
            bgmVolumeSlider = FindComponentInRoot<Slider>(pauseRoot, "BgmVolumeSlider");
        }

        if (sfxVolumeSlider == null)
        {
            sfxVolumeSlider = FindComponentInRoot<Slider>(pauseRoot, "SfxVolumeSlider");
        }

        if (bgmVolumePercentText == null)
        {
            bgmVolumePercentText = FindComponentInRoot<TextMeshProUGUI>(pauseRoot, "BgmVolumePercentText");
        }

        if (sfxVolumePercentText == null)
        {
            sfxVolumePercentText = FindComponentInRoot<TextMeshProUGUI>(pauseRoot, "SfxVolumePercentText");
        }

        if (resumeButton == null)
        {
            resumeButton = FindComponentInRoot<Button>(pauseRoot, "ResumeButton");
        }

        if (applyButton == null)
        {
            applyButton = FindComponentInRoot<Button>(pauseRoot, "ApplyButton");
        }

        if (quitGameButton == null)
        {
            quitGameButton = FindComponentInRoot<Button>(pauseRoot, "QuitGameButton");
        }
    }

    private void SetRootActive(bool active)
    {
        GameObject target = pauseRoot != null ? pauseRoot : gameObject;
        target.SetActive(active);
    }

    private static T FindComponentInRoot<T>(GameObject root, string objectName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name != objectName)
            {
                continue;
            }

            return children[i].GetComponent<T>();
        }

        return null;
    }

    private static GameObject FindInLoadedScenes(string objectName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform transform = allTransforms[i];
            if (transform.name != objectName)
            {
                continue;
            }

            if (transform.gameObject.scene.IsValid() && transform.gameObject.scene.isLoaded)
            {
                return transform.gameObject;
            }
        }

        return null;
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

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
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
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(action);
        }
    }
}
