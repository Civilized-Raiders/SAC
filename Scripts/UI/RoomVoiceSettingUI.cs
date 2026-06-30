using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방에서 내 마이크 설정을 조절하는 UI 패널입니다.(추후 보류 가능)
/// </summary>
public class RoomVoiceSettingUI : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private TextMeshProUGUI voiceSettingTitleText;

    [Header("Mic Toggle")]
    [SerializeField] private Button micToggleButton;
    [SerializeField] private Image micStateIcon;
    [SerializeField] private Sprite micOnSprite;
    [SerializeField] private Sprite micOffSprite;

    [Header("Mic Volume")]
    [SerializeField] private TextMeshProUGUI voiceVolumeLabelText;
    [SerializeField] private Slider voiceVolumeSlider;
    [SerializeField] private TextMeshProUGUI voiceVolumePercentText;

    [Header("Optional")]
    [SerializeField] private Button voiceTestButton;

    public event Action<bool> MicStateChanged;
    public event Action<float> VoiceVolumeChanged;

    private bool isMicOn = true;

    private void Start()
    {
        SetStaticTexts();
        SetMicState(isMicOn);
        UpdateVoiceVolumeText(GetSliderValue(voiceVolumeSlider));

        // 현재 기획에서는 음성 테스트 버튼을 사용하지 않는다.
        // 나중에 필요하면 버튼을 다시 켜고 테스트 기능을 연결한다.
        if (voiceTestButton != null)
        {
            voiceTestButton.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (micToggleButton != null)
        {
            micToggleButton.onClick.AddListener(OnClickMicToggleButton);
        }

        if (voiceVolumeSlider != null)
        {
            voiceVolumeSlider.onValueChanged.AddListener(OnVoiceVolumeChanged);
        }
    }

    private void OnDisable()
    {
        if (micToggleButton != null)
        {
            micToggleButton.onClick.RemoveListener(OnClickMicToggleButton);
        }

        if (voiceVolumeSlider != null)
        {
            voiceVolumeSlider.onValueChanged.RemoveListener(OnVoiceVolumeChanged);
        }
    }

    // MicToggleButton은 내 마이크 입력 On/Off를 바꾸는 로컬 UI 요청이다.
    // 다른 플레이어에게 보이는 마이크 상태는 나중에 네트워크 상태로 확정해서 표시한다.
    public void OnClickMicToggleButton()
    {
        SetMicState(!isMicOn);
        MicStateChanged?.Invoke(isMicOn);
    }

    public void SetMicState(bool micOn)
    {
        isMicOn = micOn;

        if (micStateIcon != null)
        {
            micStateIcon.sprite = micOn ? micOnSprite : micOffSprite;
            micStateIcon.enabled = micStateIcon.sprite != null;
        }
    }

    // 이 슬라이더는 내 마이크 볼륨 표시/요청용이다.
    // 실제 음성 시스템 연결은 보이스 기능이 준비된 뒤 별도로 연결한다.
    public void OnVoiceVolumeChanged(float value)
    {
        float safeValue = Mathf.Clamp01(value);
        UpdateVoiceVolumeText(safeValue);
        VoiceVolumeChanged?.Invoke(safeValue);
    }

    private void SetStaticTexts()
    {
        if (voiceSettingTitleText != null)
        {
            voiceSettingTitleText.text = "마이크";
        }

        if (voiceVolumeLabelText != null)
        {
            voiceVolumeLabelText.text = "음성 볼륨";
        }
    }

    private void UpdateVoiceVolumeText(float value)
    {
        if (voiceVolumePercentText != null)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f);
            voiceVolumePercentText.text = $"{percent}%";
        }
    }

    private static float GetSliderValue(Slider slider)
    {
        return slider != null ? slider.value : 0f;
    }
}
