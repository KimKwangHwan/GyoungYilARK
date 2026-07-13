using UnityEngine;
using UnityEngine.Pool;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] private float hitDistance = 0.3f;
    [SerializeField] private float maxLifetime = 5f;

    private Transform target;
    private float damage;
    private float elapsed;
    private IObjectPool<Projectile> pool;

    public void Launch(Transform target, float damage, IObjectPool<Projectile> pool)
    {
        this.target = target;
        this.damage = damage;
        this.pool = pool;
        elapsed = 0f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= maxLifetime || target == null)
        {
            Return();
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude <= hitDistance * hitDistance)
        {
            Hit();
            return;
        }

        transform.rotation = Quaternion.LookRotation(toTarget);
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void Hit()
    {
        //if (target.GetComponentInParent<IDamageAble>() is IDamageAble damageable)
        //    damageable.TakeDamage((int)damage);
        Return();
    }

    private void Return()
    {
        if (pool != null) pool.Release(this);
        else Destroy(gameObject);
    }
}
