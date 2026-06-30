// File: Assets/@Project/Scripts/Contents/PlayerData.cs
// Role : 플레이어 1명의 게임 데이터를 들고 있는 NetworkBehaviour.
//        InputAuthority = 해당 플레이어 본인, StateAuthority = Host.
//        현재는 로비 단계 데이터(Ready/Color/Nickname)만 다루고,
//        게임 본편이 추가되면 이 클래스에 게임 데이터가 같이 들어간다.

using System;
using Fusion;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    [Networked] public NetworkString<_16> Nickname { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public int ColorId { get; set; }
    [Networked] public int SelectedMapIndex { get; set; }
    [Networked] public NetworkBool IsRoomHost { get; set; }

    public event Action<PlayerData> Changed;

    public bool IsLocal => Object != null && Object.HasInputAuthority;
    public PlayerRef PlayerRef => Object != null ? Object.InputAuthority : PlayerRef.None;

    // 다른 클라이언트도 누가 호스트인지 알 수 있는 안전한 판별 방법:
    // Spawn 시 inputAuthority = player 였고 StateAuthority = Host 이므로,
    // Object.InputAuthority == Object.StateAuthority 면 그 PlayerData가 호스트의 것이다.
    public bool IsHostPlayer => Object != null && Object.InputAuthority == Object.StateAuthority;
    public bool IsRoomHostPlayer => IsRoomHost;
    public bool IsLocalHost => Runner != null && Runner.IsServer && IsLocal;

    private ChangeDetector changeDetector;

    public override void Spawned()
    {
        DontDestroyOnLoad(gameObject);

        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.RegisterPlayerData(this);
        }

        if (HasStateAuthority)
        {
            ColorId = -1;
            IsReady = false;
            SelectedMapIndex = 0;
            IsRoomHost = Runner != null && Runner.IsServer && Object != null && Object.InputAuthority == Runner.LocalPlayer;
            Nickname = $"Player{Object.InputAuthority.PlayerId}";
        }

        if (HasInputAuthority)
        {
            RPC_SetNickname($"Player{Object.InputAuthority.PlayerId}");
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (FusionRoomManager.Instance != null)
        {
            FusionRoomManager.Instance.UnregisterPlayerData(this);
        }
    }

    public override void Render()
    {
        if (changeDetector == null) return;

        bool hasChanges = false;
        foreach (var _ in changeDetector.DetectChanges(this))
        {
            hasChanges = true;
        }

        // ★ 변경사항 전부 소진 후 한 번만 이벤트 발생
        if (hasChanges)
            Changed?.Invoke(this);
    }

    // ===== 본인이 호출하는 변경 요청 API =====
    public void RequestToggleReady()
    {
        if (!HasInputAuthority) return;
        RPC_ToggleReady();
    }

    public void RequestSetColor(int colorId)
    {
        if (!HasInputAuthority) return;
        RPC_SetColor(colorId);
    }

    public void RequestSetNickname(string nickname)
    {
        if (!HasInputAuthority) return;
        RPC_SetNickname(nickname);
    }

    public void RequestSetSelectedMapIndex(int mapIndex)
    {
        if (!HasInputAuthority) return;

        if (HasStateAuthority && IsRoomHostPlayer)
        {
            SelectedMapIndex = Mathf.Max(0, mapIndex);
            return;
        }

        RPC_SetSelectedMapIndex(mapIndex);
    }

    // ===== RPC : InputAuthority(본인) → StateAuthority(Host) =====
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ToggleReady()
    {
        IsReady = !IsReady;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetColor(int colorId)
    {
        if (FusionRoomManager.Instance != null)
        {
            if (FusionRoomManager.Instance.IsColorTaken(colorId, PlayerRef))
            {
                return;
            }
        }
        ColorId = colorId;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetNickname(string nickname)
    {
        if (string.IsNullOrEmpty(nickname)) return;
        if (nickname.Length > 16) nickname = nickname.Substring(0, 16);
        Nickname = nickname;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetSelectedMapIndex(int mapIndex)
    {
        if (!IsRoomHostPlayer)
        {
            return;
        }

        SelectedMapIndex = Mathf.Max(0, mapIndex);
    }
}
