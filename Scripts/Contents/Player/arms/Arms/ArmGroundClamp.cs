using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ArmGroundClamp : NetworkBehaviour
{
    [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private float rayStartHeight = 1.5f;
    [SerializeField] private float rayDistance = 5f;
    [SerializeField] private float groundPadding = 0.05f;
    [SerializeField] private float minY = -5f;
    [SerializeField] private float resetHeight = 1f;

    private Rigidbody rb;
    private NetworkRigidbody3D netRb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        netRb = GetComponent<NetworkRigidbody3D>();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object != null && Object.IsValid && !HasStateAuthority)
            return;

        if (transform.position.y < minY)
        {
            ResetAboveGround();
            return;
        }

        ClampToGroundIfBelow();
    }

    private void ClampToGroundIfBelow()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * rayStartHeight;

        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            return;

        float targetY = hit.point.y + groundPadding;

        if (transform.position.y >= targetY)
            return;

        Vector3 position = transform.position;
        position.y = targetY;

        ApplyPosition(position);
        ClearVelocity();
    }

    private void ResetAboveGround()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * rayStartHeight;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            ApplyPosition(hit.point + Vector3.up * resetHeight);
        }
        else
        {
            Vector3 position = transform.position;
            position.y = resetHeight;
            ApplyPosition(position);
        }

        ClearVelocity();
    }

    private void ApplyPosition(Vector3 position)
    {
        if (netRb != null)
            netRb.Teleport(position, transform.rotation);
        else if (rb != null)
            rb.position = position;
        else
            transform.position = position;
    }

    private void ClearVelocity()
    {
        if (rb == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}