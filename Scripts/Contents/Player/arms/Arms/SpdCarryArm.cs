using UnityEngine;

public class SpdCarryArm : CarryArm
{
    [SerializeField] private float speedMultiplier = 2f;

    private PlayerMove ownerPlayerMove;
    private float originalMoveSpeed;
    private bool appliedSpeedBoost;

    public override void Equip(GameObject owner)
    {
        base.Equip(owner);

        ownerPlayerMove = owner != null ? owner.GetComponent<PlayerMove>() : null;
        if (ownerPlayerMove == null)
        {
            Debug.LogWarning("[SpdCarryArm] Owner PlayerMove를 찾지 못했습니다.");
            return;
        }

        if (appliedSpeedBoost) return;

        originalMoveSpeed = ownerPlayerMove.moveSpeed;
        ownerPlayerMove.moveSpeed = originalMoveSpeed * speedMultiplier;
        appliedSpeedBoost = true;
    }

    public override void Unequip()
    {
        if (appliedSpeedBoost && ownerPlayerMove != null)
        {
            ownerPlayerMove.moveSpeed = originalMoveSpeed;
        }

        appliedSpeedBoost = false;
        ownerPlayerMove = null;

        base.Unequip();
    }
}
