using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    [SerializeField] private AttackBehaviour _attackBehaviour;

    public void OnAttackEvent()
    {
        _attackBehaviour.OnAttack();
    }
}
