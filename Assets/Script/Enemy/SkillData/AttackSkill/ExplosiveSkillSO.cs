using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ExplosiveSkillSO : AttackSkillDataSO
{
    public GameObject effectPrefab;
    [Tooltip("폭발 이펙트 자동 반환까지의 수명(초). 이펙트 재생 길이에 맞춰 설정.")]
    public float effectLifetime = 0.8f;
    public override bool TriggerOnDeath => true;
    public override UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (owner == null || owner.Board == null) return UniTask.CompletedTask;

        // 이펙트: 죽는 위치에 스폰하고, 반환은 논블로킹(자동 수명)으로 맡긴다.
        // await로 막지 않으므로 데미지가 뒤로 밀리지 않는다.
        if (effectPrefab != null)
        {
            GameObject go = PoolManager.Instance.SpawnBudgeted(effectPrefab, owner.transform.position, Quaternion.identity);
            if (effectLifetime > 0f) PoolManager.Instance.Despawn(go, effectLifetime);
        }

        // 데미지는 지금 즉시. 온데스 스킬은 발동 직후 owner가 풀로 반환되므로,
        // await 뒤에 owner.Board를 만지면 반환/재사용된 오브젝트를 건드려 NullRef/오폭이 난다.
        Vector2Int origin = owner.Board.WorldToCell(owner.transform.position);
        foreach (Tile tile in owner.Board.GetTiles(origin, Mathf.FloorToInt(range)))
        {
            if (tile.OccupantObject == null) continue;
            if (tile.OccupantObject.GetComponentInParent<IDamageAble>() is IDamageAble target)
                target.TakeDamage(Mathf.FloorToInt(damage));
        }
        return UniTask.CompletedTask;
    }
}