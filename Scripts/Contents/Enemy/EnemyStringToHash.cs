using UnityEngine;

public static class EnemyStringToHash
{
    public static readonly int Idle = Animator.StringToHash("Idle");
    public static readonly int Move = Animator.StringToHash("Move");
    public static readonly int Attack = Animator.StringToHash("Attack");
    public static readonly int Death = Animator.StringToHash("Death");
    public static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");

}
