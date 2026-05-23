using UnityEngine;
using UnityEngine.UI;

public class TitlePanel : UIPanel
{
    [SerializeField] private UIFlowController flowController;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    private void Awake()
    {
        AutoBind();
    }

    private void OnEnable()
    {
        BindButtons();
    }

    public void OnCreateRoomClicked()
    {
        if (flowController != null)
        {
            flowController.ShowCreateRoom();
        }
    }

    public void OnJoinRoomClicked()
    {
        if (flowController != null)
        {
            flowController.ShowJoinRoom();
        }
    }

    public void OnSettingsClicked()
    {
        if (flowController != null)
        {
            flowController.ShowSettings();
        }
    }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void AutoBind()
    {
        flowController = flowController != null ? flowController : GetComponentInParent<UIFlowController>(true);
        createRoomButton = createRoomButton != null ? createRoomButton : FindButton("CreateRoom");
        joinRoomButton = joinRoomButton != null ? joinRoomButton : FindButton("JoinRoom");
        settingsButton = settingsButton != null ? settingsButton : FindButton("Settings");
        exitButton = exitButton != null ? exitButton : FindButton("Exit");
    }

    private void BindButtons()
    {
        AddListener(createRoomButton, OnCreateRoomClicked);
        AddListener(joinRoomButton, OnJoinRoomClicked);
        AddListener(settingsButton, OnSettingsClicked);
        AddListener(exitButton, OnExitClicked);
    }

    private Button FindButton(string childName)
    {
        Transform child = UIFlowController.FindChild(transform, childName);
        return child != null ? child.GetComponent<Button>() : null;
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
}
