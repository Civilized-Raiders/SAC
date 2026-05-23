using System.Collections.Generic;
using UnityEngine;

public class UIFlowController : MonoBehaviour
{
    [SerializeField] private UIPanel titlePanel;
    [SerializeField] private UIPanel createRoomPanel;
    [SerializeField] private UIPanel joinRoomPanel;
    [SerializeField] private UIPanel settingsPanel;
    [SerializeField] private bool showTitleOnStart = true;

    private readonly List<UIPanel> panels = new List<UIPanel>();
    private UIPanel currentPopupPanel;

    public UIPanel TitlePanel => titlePanel;
    public UIPanel CreateRoomPanel => createRoomPanel;
    public UIPanel JoinRoomPanel => joinRoomPanel;
    public UIPanel SettingsPanel => settingsPanel;

    private void Awake()
    {
        AutoBindPanels();
    }

    private void Start()
    {
        if (titlePanel != null && showTitleOnStart)
        {
            titlePanel.Show();
        }

        HidePopupPanels();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GoBack();
        }
    }

    public void ShowTitle()
    {
        if (titlePanel != null)
        {
            titlePanel.Show();
        }

        HidePopupPanels();
    }

    public void ShowCreateRoom()
    {
        ShowPanel(createRoomPanel);
    }

    public void ShowJoinRoom()
    {
        ShowPanel(joinRoomPanel);
    }

    public void ShowSettings()
    {
        ShowPanel(settingsPanel);
    }

    public void GoBack()
    {
        if (currentPopupPanel != null)
        {
            currentPopupPanel.Hide();
            currentPopupPanel = null;
        }
    }

    public void ShowPanel(UIPanel panel)
    {
        if (panel == null)
        {
            return;
        }

        foreach (UIPanel uiPanel in panels)
        {
            if (uiPanel == null || uiPanel == titlePanel)
            {
                continue;
            }

            if (uiPanel == panel)
            {
                uiPanel.Show();
            }
            else
            {
                uiPanel.Hide();
            }
        }

        if (titlePanel != null)
        {
            titlePanel.Show();
        }

        currentPopupPanel = panel;
    }

    private void HidePopupPanels()
    {
        foreach (UIPanel uiPanel in panels)
        {
            if (uiPanel != null && uiPanel != titlePanel)
            {
                uiPanel.Hide();
            }
        }

        currentPopupPanel = null;
    }

    private void AutoBindPanels()
    {
        titlePanel = titlePanel != null ? titlePanel : FindPanel("TitlePanel");
        createRoomPanel = createRoomPanel != null ? createRoomPanel : FindPanel("CreateRoomPanel");
        joinRoomPanel = joinRoomPanel != null ? joinRoomPanel : FindPanel("JoinRoomPanel");
        settingsPanel = settingsPanel != null ? settingsPanel : FindPanel("SettingsPanel");

        panels.Clear();
        AddPanel(titlePanel);
        AddPanel(createRoomPanel);
        AddPanel(joinRoomPanel);
        AddPanel(settingsPanel);
    }

    private void AddPanel(UIPanel panel)
    {
        if (panel != null && !panels.Contains(panel))
        {
            panels.Add(panel);
        }
    }

    private UIPanel FindPanel(string panelName)
    {
        Transform child = FindChild(transform, panelName);
        if (child == null)
        {
            return null;
        }

        return child.GetComponent<UIPanel>() ?? child.gameObject.AddComponent<UIPanel>();
    }

    public static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }
}
