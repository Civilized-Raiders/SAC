public class DeadBehaviour : EnemyBehaviour
{
    private float _despawnTime = 5f;
    private bool _isDespawn;
    protected override void OnEnterState()
    {
        _isDespawn = false;
    }

    protected override void OnFixedUpdate()
    {
        if (_isDespawn)
            return;
        
        if (_despawnTime < Machine.StateTime)
        {
            _isDespawn = true;
            Runner.Despawn(Object);
        }
    }   

    protected override void OnRender()
    {
        _owner.animator.SetTrigger(EnemyStringToHash.Death);
    }
}