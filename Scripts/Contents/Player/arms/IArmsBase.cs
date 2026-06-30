using UnityEngine;

public enum ArmType
{
    Carry, Flash, ForkLift,Gun
}

public interface IArmsBase
{
    string ArmName { get; }
    ArmType ArmType { get; }
    bool IsEquipped { get; }
    bool IsUsing { get; }
    float BatteryDrainRate { get; }

    void Equip(GameObject owner);
    void Unequip();

    void UseStart();
    void UseUpdate();
    void UseEnd();

    void SetWorldItemVisible(bool visible);
}
