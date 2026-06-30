using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class GroundClamp : NetworkBehaviour
{
    [SerializeField] private float minY = 0f;
    [SerializeField] private float resetY = 1f;

    protected NetworkRigidbody3D netRb;
    protected Rigidbody rb;

    protected virtual void Awake()
    {
        netRb = GetComponent<NetworkRigidbody3D>();
        rb = GetComponent<Rigidbody>();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object != null && Object.IsValid && !HasStateAuthority)
            return;

        if (transform.position.y > minY)
            return;

        Vector3 position = transform.position;
        position.y = resetY;

        if (netRb != null)
            netRb.Teleport(position, transform.rotation);
        else
            transform.position = position;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}