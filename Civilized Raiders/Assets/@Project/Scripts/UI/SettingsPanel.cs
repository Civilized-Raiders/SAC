using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : UIPanel
{
    [SerializeField] private UIFlowController flowController;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        AutoBind();
    }

    private void OnEnable()
    {
        BindButtons();
    }

    public void OnCloseClicked()
    {
        if (flowController != null)
        {
            flowController.GoBack();
        }
    }

    private void AutoBind()
    {
        flowController = flowController != null ? flowController : GetComponentInParent<UIFlowController>(true);
        Transform close = UIFlowController.FindChild(transform, "ESC");
        closeButton = closeButton != null ? closeButton : close != null ? close.GetComponent<Button>() : null;
    }

    private void BindButtons()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(OnCloseClicked);
        closeButton.onClick.AddListener(OnCloseClicked);
    }
}
