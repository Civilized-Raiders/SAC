using Fusion;

public class ArmOwnership : NetworkBehaviour
{
    [Networked] public NetworkId OwnerId { get; set; }

    public bool IsOwned => OwnerId.IsValid;

    public bool TryClaim(NetworkObject owner)
    {
        if (!HasStateAuthority) return false;
        if (owner == null) return false;
        if (OwnerId.IsValid) return false;

        OwnerId = owner.Id;
        return true;
    }

    public bool IsOwnedBy(NetworkObject owner)
    {
        return owner != null && OwnerId == owner.Id;
    }

    public void Release(NetworkObject owner)
    {
        if (!HasStateAuthority) return;
        if (!IsOwnedBy(owner)) return;

        OwnerId = default;
    }
}