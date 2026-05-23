using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class LobbyUISkeletonCreator
{
    private const string MenuPath = "Tools/Project/Create Lobby UI Skeleton";

    [MenuItem(MenuPath)]
    public static void CreateLobbyUISkeleton()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            canvas = CreateCanvas();
        }

        UIFlowController flowController = canvas.GetComponent<UIFlowController>();
        if (flowController == null)
        {
            flowController = canvas.gameObject.AddComponent<UIFlowController>();
        }

        EnsureEventSystem();

        GameObject titlePanel = EnsurePanel(canvas.transform, "TitlePanel");
        EnsureComponent<TitlePanel>(titlePanel);
        EnsureButton(titlePanel.transform, "CreateRoom", "Create Room", new Vector2(0f, 120f));
        EnsureButton(titlePanel.transform, "JoinRoom", "Join Room", new Vector2(0f, 40f));
        EnsureButton(titlePanel.transform, "Settings", "Settings", new Vector2(0f, -40f));
        EnsureButton(titlePanel.transform, "Exit", "Exit", new Vector2(0f, -120f));

        GameObject createRoomPanel = EnsurePanel(canvas.transform, "CreateRoomPanel");
        EnsureComponent<CreateRoomPanel>(createRoomPanel);
        EnsureLabel(createRoomPanel.transform, "CreateRoomText", "Create Room", new Vector2(0f, 170f));
        EnsureButton(createRoomPanel.transform, "PublicRoom", "Public Room", new Vector2(0f, 80f));
        EnsureButton(createRoomPanel.transform, "PrivateRoom", "Private Room", new Vector2(0f, 0f));
        EnsureInput(createRoomPanel.transform, "PrivateRoomInput", "Private Room Code", new Vector2(0f, -80f));
        EnsureButton(createRoomPanel.transform, "PrivateRoomConfirm", "Confirm", new Vector2(0f, -160f));
        EnsureButton(createRoomPanel.transform, "ESC", "Back", new Vector2(-300f, 210f), new Vector2(120f, 48f));

        GameObject joinRoomPanel = EnsurePanel(canvas.transform, "JoinRoomPanel");
        EnsureComponent<JoinRoomPanel>(joinRoomPanel);
        EnsureLabel(joinRoomPanel.transform, "JoinRoomText", "Join Room", new Vector2(0f, 200f));
        EnsureListRoot(joinRoomPanel.transform, "RoomList", new Vector2(-160f, 20f));
        EnsureInput(joinRoomPanel.transform, "Room Code", "Private Room Code", new Vector2(190f, 30f));
        EnsureButton(joinRoomPanel.transform, "Join", "Join", new Vector2(190f, -50f));
        EnsureButton(joinRoomPanel.transform, "ESC", "Back", new Vector2(-300f, 230f), new Vector2(120f, 48f));

        GameObject roomPanel = EnsurePanel(canvas.transform, "RoomPanel");
        EnsureComponent<RoomPanel>(roomPanel);
        EnsureLabel(roomPanel.transform, "RoomCodeText", "Room Code : ----", new Vector2(0f, 200f));
        EnsureListRoot(roomPanel.transform, "PlayerListRoot", new Vector2(0f, 60f));
        EnsureButton(roomPanel.transform, "ReadyButton", "Ready", new Vector2(-180f, -180f));
        EnsureButton(roomPanel.transform, "HostStartButton", "Host Start", new Vector2(0f, -180f));
        EnsureButton(roomPanel.transform, "LeaveButton", "Leave", new Vector2(180f, -180f));

        GameObject settingsPanel = EnsurePanel(canvas.transform, "SettingsPanel");
        EnsureComponent<SettingsPanel>(settingsPanel);
        EnsureLabel(settingsPanel.transform, "SettingsText", "Settings", new Vector2(0f, 160f));
        EnsureButton(settingsPanel.transform, "ESC", "Back", new Vector2(-300f, 210f), new Vector2(120f, 48f));

        titlePanel.SetActive(true);
        createRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(false);
        roomPanel.SetActive(false);
        settingsPanel.SetActive(false);

        Selection.activeGameObject = canvas.gameObject;
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static GameObject EnsurePanel(Transform parent, string panelName)
    {
        Transform existing = UIFlowController.FindChild(parent, panelName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.07f, 0.08f, 0.1f, 0.92f);

        return panel;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Button EnsureButton(Transform parent, string buttonName, string label, Vector2 position)
    {
        return EnsureButton(parent, buttonName, label, position, new Vector2(240f, 56f));
    }

    private static Button EnsureButton(Transform parent, string buttonName, string label, Vector2 position, Vector2 size)
    {
        Transform existing = UIFlowController.FindChild(parent, buttonName);
        if (existing != null)
        {
            Button existingButton = existing.GetComponent<Button>();
            return existingButton != null ? existingButton : existing.gameObject.AddComponent<Button>();
        }

        GameObject buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), position, size);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.18f);

        EnsureLabel(buttonObject.transform, "Text", label, Vector2.zero, size);
        return buttonObject.GetComponent<Button>();
    }

    private static TextMeshProUGUI EnsureLabel(Transform parent, string labelName, string text, Vector2 position)
    {
        return EnsureLabel(parent, labelName, text, position, new Vector2(420f, 52f));
    }

    private static TextMeshProUGUI EnsureLabel(Transform parent, string labelName, string text, Vector2 position, Vector2 size)
    {
        Transform existing = UIFlowController.FindChild(parent, labelName);
        if (existing != null)
        {
            TextMeshProUGUI existingLabel = existing.GetComponent<TextMeshProUGUI>();
            if (existingLabel != null)
            {
                return existingLabel;
            }
        }

        GameObject labelObject = new GameObject(labelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        SetRect(labelObject.GetComponent<RectTransform>(), position, size);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return label;
    }

    private static TMP_InputField EnsureInput(Transform parent, string inputName, string placeholder, Vector2 position)
    {
        Transform existing = UIFlowController.FindChild(parent, inputName);
        if (existing != null)
        {
            TMP_InputField existingInput = existing.GetComponent<TMP_InputField>();
            return existingInput != null ? existingInput : existing.gameObject.AddComponent<TMP_InputField>();
        }

        GameObject inputObject = new GameObject(inputName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(parent, false);
        SetRect(inputObject.GetComponent<RectTransform>(), position, new Vector2(300f, 56f));

        Image image = inputObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.14f);

        TextMeshProUGUI text = EnsureLabel(inputObject.transform, "Text", string.Empty, Vector2.zero, new Vector2(280f, 46f));
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontSize = 22f;

        TextMeshProUGUI placeholderText = EnsureLabel(inputObject.transform, "Placeholder", placeholder, Vector2.zero, new Vector2(280f, 46f));
        placeholderText.alignment = TextAlignmentOptions.MidlineLeft;
        placeholderText.fontSize = 22f;
        placeholderText.color = new Color(1f, 1f, 1f, 0.45f);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholderText;
        return input;
    }

    private static Transform EnsureListRoot(Transform parent, string listName, Vector2 position)
    {
        Transform existing = UIFlowController.FindChild(parent, listName);
        if (existing != null)
        {
            return existing;
        }

        GameObject listObject = new GameObject(listName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listObject.transform.SetParent(parent, false);
        SetRect(listObject.GetComponent<RectTransform>(), position, new Vector2(320f, 220f));

        VerticalLayoutGroup layout = listObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = listObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return listObject.transform;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
