using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomPanel : UIPanel
{
    [SerializeField] private TextMeshProUGUI roomCodeText;
    [SerializeField] private Transform playerListRoot;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button hostStartButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private string titleSceneName = "Title";

    private bool isReady;

    private void Awake()
    {
        AutoBind();
    }

    private void OnEnable()
    {
        BindButtons();
        //SetPlayers(new[] { "Host - Not Ready", "Player - Ready" });

        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.OnPlayerDataSpawned += RefreshPlayerList;
            FusionRoomManager.Instance.OnPlayerDataDespawned += RefreshPlayerList;
            RefreshPlayerList(null);
        }

        SetRoomCode(FusionRoomManager.Instance?.CurrentRoomInfo.RoomCode ?? "PUBLIC");

    }

    private void OnDisable()
    {
        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.OnPlayerDataSpawned -= RefreshPlayerList;
            FusionRoomManager.Instance.OnPlayerDataDespawned -= RefreshPlayerList;
        }
    }
    private void RefreshPlayerList(PlayerData _)
    {
        if (FusionRoomManager.Instance == null) return;

        var list = FusionRoomManager.Instance.PlayerDataList;
        var names = new List<string>();

        foreach (var p in list)
        {
            if (p == null) continue;
            // ★ 자기 자신이면 {ME} 표시
            string name = p.IsLocal
                ? $"{p.Nickname} {{ME}}"
                : p.Nickname.ToString();
            names.Add(name);
        }

        SetPlayers(names);
    }

    public void OnReadyClicked()
    {
        isReady = !isReady;
        SetReadyState(isReady);
    }

    public void OnHostStartClicked()
    {
        Debug.Log("Host start request.");
    }

    public void OnLeaveClicked()
    {
        Debug.Log(" Leave room request.");
        SceneManager.LoadScene(titleSceneName);
    }

    public void SetRoomCode(string roomCode)
    {
        if (roomCodeText != null)
        {
            roomCodeText.text = $"Room Code : {roomCode}";
        }
    }

    public void SetPlayers(IReadOnlyList<string> players)
    {
        if (playerListRoot == null)
        {
            return;
        }

        for (int i = playerListRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(playerListRoot.GetChild(i).gameObject);
        }

        foreach (string player in players)
        {
            CreatePlayerText(player);
        }
    }

    public void SetReadyState(bool ready)
    {
        isReady = ready;
        TextMeshProUGUI label = readyButton != null ? readyButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (label != null)
        {
            label.text = ready ? "Cancel Ready" : "Ready";
        }
    }

    public void SetHostControlsVisible(bool isHost)
    {
        if (hostStartButton != null)
        {
            hostStartButton.gameObject.SetActive(isHost);
        }
    }

    private void CreatePlayerText(string player)
    {
        GameObject textObject = new GameObject(player, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(playerListRoot, false);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = player;
        label.fontSize = 24f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
    }

    private void AutoBind()
    {
        roomCodeText = roomCodeText != null ? roomCodeText : FindText("RoomCodeText");
        playerListRoot = playerListRoot != null ? playerListRoot : FindTransform("PlayerListRoot");
        readyButton = readyButton != null ? readyButton : FindButton("ReadyButton");
        hostStartButton = hostStartButton != null ? hostStartButton : FindButton("HostStartButton");
        leaveButton = leaveButton != null ? leaveButton : FindButton("LeaveButton");
    }

    private void BindButtons()
    {
        AddListener(readyButton, OnReadyClicked);
        AddListener(hostStartButton, OnHostStartClicked);
        AddListener(leaveButton, OnLeaveClicked);
    }

    private Transform FindTransform(string childName)
    {
        return UIFlowController.FindChild(transform, childName);
    }

    private TextMeshProUGUI FindText(string childName)
    {
        Transform child = FindTransform(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private Button FindButton(string childName)
    {
        Transform child = FindTransform(childName);
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
