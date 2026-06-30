using UnityEngine;

public class MagnetCarryArm : CarryArm
{
    [SerializeField] private float heightMultiplier = 5f;

    private CapsuleCollider detectCapsule;
    private float originalHeight;
    private Vector3 originalCenter;
    private bool appliedDetectHeight;

    public override void Equip(GameObject owner)
    {
        base.Equip(owner);
        ApplyMagnetDetectHeight();
    }

    public override void Unequip()
    {
        RestoreMagnetDetectHeight();
        base.Unequip();
    }

    private void ApplyMagnetDetectHeight()
    {
        if (appliedDetectHeight) return;

        detectCapsule = CarryDetectTrigger as CapsuleCollider;
        if (detectCapsule == null) return;

        originalHeight = detectCapsule.height;
        originalCenter = detectCapsule.center;

        Vector3 center = originalCenter;
        center.z = originalCenter.z * heightMultiplier;

        detectCapsule.height = originalHeight * heightMultiplier;
        detectCapsule.center = center;

        appliedDetectHeight = true;
    }

    private void RestoreMagnetDetectHeight()
    {
        if (!appliedDetectHeight || detectCapsule == null) return;

        detectCapsule.height = originalHeight;
        detectCapsule.center = originalCenter;

        detectCapsule = null;
        appliedDetectHeight = false;
    }
}