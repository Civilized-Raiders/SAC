using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class InteractionText : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectDistance = 8f;
    [SerializeField] private LayerMask detectMask = ~0;
    [SerializeField] private bool useScreenCenterWhenCursorLocked = true;
    [SerializeField] private float hideDelay = 0.12f;

    [Header("UI")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, -48f);
    [SerializeField] private Vector2 panelSize = new Vector2(320f, 40f);
    [SerializeField] private int fontSize = 20;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);

    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    private Camera cachedCamera;
    private Canvas canvas;
    private RectTransform panelRect;
    private Text label;
    private Transform cachedLocalPlayer;
    private float hideTime;

    private void Start()
    {
        EnsureUi();
        HideImmediately();
    }

    private void Update()
    {
        EnsureUi();

        Camera targetCamera = GetTargetCamera();
        if (targetCamera == null)
        {
            UpdateHideState();
            return;
        }

        Vector2 screenPoint = GetPointerScreenPoint();
        if (TryFindMessage(targetCamera, screenPoint, out string message))
        {
            ShowLabel(message, screenPoint);
            hideTime = Time.unscaledTime + hideDelay;
            return;
        }

        UpdateHideState();
    }

    private Camera GetTargetCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
        {
            return cachedCamera;
        }

        cachedCamera = Camera.main;
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
        {
            return cachedCamera;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
            {
                cachedCamera = cameras[i];
                return cachedCamera;
            }
        }

        return null;
    }

    private Vector2 GetPointerScreenPoint()
    {
        bool useCenter = useScreenCenterWhenCursorLocked &&
                         ((Cursor.lockState == CursorLockMode.Locked) ||
                          (Cursor.lockState == CursorLockMode.Confined && !Cursor.visible));

        if (useCenter)
        {
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        return Input.mousePosition;
    }

    private bool TryFindMessage(Camera targetCamera, Vector2 screenPoint, out string message)
    {
        message = null;

        Ray ray = targetCamera.ScreenPointToRay(screenPoint);
        RaycastHit[] hits = Physics.RaycastAll(ray, detectDistance, detectMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (TryBuildMessage(hits[i].collider, out message))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryBuildMessage(Collider hitCollider, out string message)
    {
        message = null;

        if (hitCollider == null)
        {
            return false;
        }

        SettlementButton settlementButton = hitCollider.GetComponentInParent<SettlementButton>();
        if (settlementButton != null && IsSettlementAvailable(settlementButton))
        {
            message = "[F] Sell";
            return true;
        }

        ShopInteractable shopInteractable = hitCollider.GetComponentInParent<ShopInteractable>();
        if (shopInteractable != null && IsShopAvailable(shopInteractable))
        {
            message = "[F] Open Shop";
            return true;
        }

        ArmInteractable armInteractable = hitCollider.GetComponentInParent<ArmInteractable>();
        if (armInteractable != null)
        {
            message = $"[E] Equip {GetDisplayName(armInteractable.gameObject.name)}";
            return true;
        }

        Carryable carryable = hitCollider.GetComponentInParent<Carryable>();
        if (carryable != null)
        {
            message = $"[LMB] Pick Up {GetDisplayName(carryable.gameObject.name)}";
            return true;
        }

        Interactable interactable = hitCollider.GetComponentInParent<Interactable>();
        if (interactable != null)
        {
            message = GetInteractableMessage(interactable);
            return true;
        }

        return false;
    }

    private bool IsSettlementAvailable(SettlementButton settlementButton)
    {
        Transform localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            return false;
        }

        float range = GetPrivateFloat(settlementButton, "settleRange", 3f);
        return Vector3.Distance(localPlayer.position, settlementButton.transform.position) <= range;
    }

    private bool IsShopAvailable(ShopInteractable shopInteractable)
    {
        return GetPrivateBool(shopInteractable, "_isPlayerInRange");
    }

    private Transform GetLocalPlayer()
    {
        if (cachedLocalPlayer != null)
        {
            return cachedLocalPlayer;
        }

        Interaction[] interactions = FindObjectsByType<Interaction>(FindObjectsSortMode.None);
        for (int i = 0; i < interactions.Length; i++)
        {
            Interaction interaction = interactions[i];
            if (interaction == null)
            {
                continue;
            }

            if (interaction.Object != null && interaction.Object.IsValid)
            {
                if (interaction.Object.HasInputAuthority)
                {
                    cachedLocalPlayer = interaction.transform;
                    return cachedLocalPlayer;
                }
            }
            else
            {
                cachedLocalPlayer = interaction.transform;
                return cachedLocalPlayer;
            }
        }

        return null;
    }

    private string GetInteractableMessage(Interactable interactable)
    {
        string typeName = interactable.GetType().Name;
        string objectName = GetDisplayName(interactable.gameObject.name);

        if (typeName == "Store")
        {
            return $"[E] Open {objectName}";
        }

        if (typeName == "ChargingStation")
        {
            return $"[E] Use {objectName}";
        }

        if (typeName == "SettlementStation")
        {
            return "[F] Sell";
        }

        return $"[E] Interact {objectName}";
    }

    private static string GetDisplayName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return "Object";
        }

        string clean = rawName.Replace("(Clone)", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "Object" : clean;
    }

    private static bool GetPrivateBool(object target, string fieldName)
    {
        if (target == null)
        {
            return false;
        }

        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        if (field != null && field.GetValue(target) is bool value)
        {
            return value;
        }

        return false;
    }

    private static float GetPrivateFloat(object target, string fieldName, float fallback)
    {
        if (target == null)
        {
            return fallback;
        }

        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        if (field != null && field.GetValue(target) is float value)
        {
            return value;
        }

        return fallback;
    }

    private void EnsureUi()
    {
        if (canvas != null && panelRect != null && label != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("InteractionTextCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);


        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("InteractionHint", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);

        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.sizeDelta = panelSize;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = backgroundColor;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 16f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        label = textObject.GetComponent<Text>();
        label.font = CreateRuntimeFont();
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = textColor;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.supportRichText = false;
    }

    private Font CreateRuntimeFont()
    {
        string[] preferredFonts =
        {
            "Malgun Gothic",
            "Apple SD Gothic Neo",
            "NanumGothic",
            "Arial",
            "Segoe UI"
        };

        Font font = Font.CreateDynamicFontFromOSFont(preferredFonts, fontSize);
        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void ShowLabel(string message, Vector2 screenPoint)
    {
        if (panelRect == null || label == null || string.IsNullOrWhiteSpace(message))
            return;

        panelRect.gameObject.SetActive(true);
        label.text = message;

        // screen → canvas local 좌표로 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            screenPoint + screenOffset,
            null, // ScreenSpaceOverlay는 null
            out Vector2 localPoint
        );
        panelRect.anchoredPosition = localPoint;
        //if (panelRect == null || label == null || string.IsNullOrWhiteSpace(message))
        //{
        //    return;
        //}

        //panelRect.gameObject.SetActive(true);
        //label.text = message;
        //panelRect.position = screenPoint + screenOffset;
    }

    private void UpdateHideState()
    {
        if (Time.unscaledTime >= hideTime)
        {
            HideImmediately();
        }
    }

    private void HideImmediately()
    {
        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(false);
        }
    }
}
