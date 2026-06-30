using Fusion.Addons.FSM;

public class EnemyBehaviour : StateBehaviour
{
    protected EnemyAI _owner;

    public void SetEnemyBase(EnemyAI enemyBaseAI)
    {
        _owner = enemyBaseAI;
    }
}
