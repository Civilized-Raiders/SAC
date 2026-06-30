using Fusion;
using UnityEngine;

public static class GameplayInputGuard
{
    public static bool IsBlocked { get; private set; }

    private static int blockRequestCount;

    public static void SetBlocked(bool blocked)
    {
        if (blocked)
        {
            blockRequestCount++;
        }
        else
        {
            blockRequestCount = Mathf.Max(0, blockRequestCount - 1);
        }

        IsBlocked = blockRequestCount > 0;
        ApplyInputProviderState(IsBlocked);
        ApplyCursorState(IsBlocked);
    }

    public static void ResetBlocked()
    {
        blockRequestCount = 0;
        IsBlocked = false;
        ApplyInputProviderState(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void ApplyCurrentCursorState()
    {
        ApplyCursorState(IsBlocked);
    }

    private static void ApplyInputProviderState(bool blocked)
    {
        PlayerInputProvider[] providers = Object.FindObjectsByType<PlayerInputProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < providers.Length; i++)
        {
            PlayerInputProvider provider = providers[i];
            if (provider == null)
            {
                continue;
            }

            NetworkObject owner = provider.GetComponent<NetworkObject>();
            if (owner == null)
            {
                owner = provider.GetComponentInParent<NetworkObject>();
            }

            if (owner != null && owner.IsValid && !owner.HasInputAuthority)
            {
                continue;
            }

            provider.SetInputBlocked(blocked);
        }
    }

    private static void ApplyCursorState(bool blocked)
    {
        Cursor.lockState = blocked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = blocked;
    }
}
