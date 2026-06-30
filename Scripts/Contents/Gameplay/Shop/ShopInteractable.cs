using System;
using UnityEngine;
using Fusion;
using Unity.VisualScripting;

public class ShopInteractable : NetworkBehaviour
{

    public PlayerMove playerMove;
    
    [Header("Interaction Setting")] [SerializeField]
    private KeyCode interactKey = KeyCode.F;

    [Header("Connect UI")] [SerializeField] private ShopUIVisibility shopPanelUI;

    private bool _isPlayerInRange = false;


    public override void Spawned()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {

        PlayerMove move = other.GetComponentInParent<PlayerMove>();
        if (move == null) return;

        NetworkObject playerObject = move.GetComponent<NetworkObject>();
        if (playerObject == null) return;

        if (!Object.HasStateAuthority) return;

        Rpc_SetShopRange(playerObject.InputAuthority, true);

    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMove move = other.GetComponentInParent<PlayerMove>();
        if (move == null) return;

        NetworkObject playerObject = move.GetComponent<NetworkObject>();
        if (playerObject == null) return;

        if (!Object.HasStateAuthority) return;

        Rpc_SetShopRange(playerObject.InputAuthority, false);
    }
    

    private void Update()
    {
        if (Input.GetKeyDown(interactKey))
        {

            if (!_isPlayerInRange || playerMove == null) return;
            


            if (!shopPanelUI.IsOpen)
            {

                OpenShop();
            }
                
            else
                CloseShop();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if(shopPanelUI.IsOpen)
            CloseShop();
        }
            
    }
    public void OpenShop()
    {
        if (shopPanelUI.IsOpen) return;

        if (playerMove != null)
            playerMove.enableCameraMove = false;

        GameplayInputGuard.SetBlocked(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        shopPanelUI.OpenShopPanel();
    }
    public void CloseShop()
    {
        if (!shopPanelUI.IsOpen) return;

        if (playerMove != null)
            playerMove.enableCameraMove = true;

        GameplayInputGuard.SetBlocked(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        shopPanelUI.CloseShopPanel();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SetShopRange(PlayerRef targetPlayer, bool inRange)
    {
        if (Runner.LocalPlayer != targetPlayer) return;

        _isPlayerInRange = inRange;

        if (!inRange)
        {
            CloseShop();
            playerMove = null;
        }
        else
        {
            playerMove = FindLocalPlayer();

            PlayerDeathHandler deathHandler = playerMove != null
                ? playerMove.GetComponent<PlayerDeathHandler>()
                : null;

            if (deathHandler != null)
                deathHandler.OnDied += CloseShop;
        }
    }

    private PlayerMove FindLocalPlayer()
    {
        PlayerMove[] players = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);

        foreach (PlayerMove move in players)
        {
            NetworkObject netObj = move.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
                return move;
        }

        return null;
    }
}

