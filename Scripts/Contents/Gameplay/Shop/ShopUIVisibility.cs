using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopUIVisibility : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject shopPanel;

    [Header("Header")]
    [SerializeField] private TMP_Text currentGoldText;
    [SerializeField] private Button closeButton;
    [SerializeField] private List<Image> itemIconImage;

    [Header("Item")]
    [SerializeField] private List<ShopItemData> itemData;
    [SerializeField] private List<TMP_Text> itemBuyPriceTexts;
    [SerializeField] private List<Button> itemBuyButtons;


    private NetworkGameManager networkGameManager;
    private bool _isOpen = false;
    public bool IsOpen { get; private set; }

   

    private void Start()
    {
        BindButtons();
        shopPanel.SetActive(false);
        IsOpen = false;
        
    }

    private void Update()
    {
        if (IsOpen)
        {
            UpdateMoneyUI();
        }
    }

    private void ResolveNetworkGameManager()
    {
        if (networkGameManager != null &&
            networkGameManager.Object != null &&
            networkGameManager.Object.IsValid)
        {
            return;
        }

        networkGameManager = NetworkGameManager.Instance;

        if (networkGameManager == null ||
            networkGameManager.Object == null ||
            !networkGameManager.Object.IsValid)
        {
            networkGameManager = FindFirstObjectByType<NetworkGameManager>();
        }
    }


    private void UpdateMoneyUI()
    {
        if (networkGameManager == null)
            return;
        if (currentGoldText == null)
            return;
        currentGoldText.text = $"{networkGameManager.CurrentMoney.ToString()} G";
        Debug.Log(networkGameManager.CurrentMoney);
    }
    private void IconSetting()
    {
        for (int i = 0; i < itemData.Count; i++)
        {
            itemIconImage[i].sprite = itemData[i].itemIcon;
        }
    }
    private void UpdateItemPriceUI()
    {
        for (int i = 0; i < Mathf.Min(itemData.Count, itemBuyPriceTexts.Count); i++)
        {
            itemBuyPriceTexts[i].text = $"{itemData[i].itemBuyPrice.ToString()} G";
        }
    }

    private void BindButtons()
    {
        int count = Mathf.Min(itemData.Count, itemBuyButtons.Count);
        for(int i=0; i<count; i++)
        {
            int index = i;
            Button button = itemBuyButtons[i];
            if(button == null)
            {
                continue;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => BuyItem(index));
        }
    }
    private void BuyItem(int index)
    {
        if (index < 0 || index >= itemData.Count) return;
        if (ShopManager.Instance == null) return;
        ShopManager.Instance.BuyItem(itemData[index]);
        UpdateMoneyUI();
    }
    public void OpenShopPanel()
    {
        shopPanel.SetActive(true);
        IsOpen = true;

        ResolveNetworkGameManager();

        IconSetting();
        UpdateMoneyUI();
        UpdateItemPriceUI();
    }

    public void CloseShopPanel()
    {

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        
        shopPanel.SetActive(false);
        IsOpen = false;
    }
}