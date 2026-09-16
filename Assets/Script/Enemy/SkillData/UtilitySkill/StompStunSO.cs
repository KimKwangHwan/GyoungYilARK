using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 내려찍기 스턴. 주변 칸의 영웅에게 Stun_Basic을 건다 — 피해는 주지 않으므로 Attack이 아니라 Utility다
// (SpiritofSnow가 빙결을 뿌리는 것과 같은 구조). 스턴 시간은 DebuffTable의 Stun_Basic이 들고 있고,
// 표에서 쓰는 값은 Range(스턴을 뿌릴 반경)와 Cooldown뿐이다.
public class StompStunSO : UtilitySkillDataSO
{
    private string StunId = "Stun_Basic";
    private DebuffSO debuffSO;
    public GameObject shockWaveEffect;
    private string skillstate = "Skill";
    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (owner == null || owner.IsDead || owner.animator == null) return;
        if (owner.Board == null) return;   
        debuffSO = DebuffLoader.Get(StunId);
        if (debuffSO == null) return;
        owner.MovementSuspended = true;
        try
        {
            owner.animator.SetTrigger(skillstate);
            await WaitForAnimationEnd(owner, skillstate, 3f, token);
            GameObject go = PoolManager.Instance.SpawnBudgeted(shockWaveEffect,owner.transform.position,shockWaveEffect.transform.rotation);
            if (go != null) go.transform.localScale = Vector3.one;
            await UniTask.Delay(TimeSpan.FromSeconds(0.25f));
            GameObject go1 = PoolManager.Instance.SpawnBudgeted(shockWaveEffect,owner.transform.position,shockWaveEffect.transform.rotation);
            if (go1 != null) go1.transform.localScale = new Vector3(2f,2f,1f);
            await UniTask.Delay(TimeSpan.FromSeconds(0.25f));
            GameObject go2 = PoolManager.Instance.SpawnBudgeted(shockWaveEffect,owner.transform.position,shockWaveEffect.transform.rotation);
            if (go2 != null) go2.transform.localScale = new Vector3(3f,3f,1f);
            await UniTask.Delay(TimeSpan.FromSeconds(0.25f));
            PoolManager.Instance.Despawn(go);
            PoolManager.Instance.Despawn(go1);
            PoolManager.Instance.Despawn(go2);
            if (owner == null || owner.IsDead || owner.Board == null) return;
            var hit = new HashSet<GameObject>();
            
            int cellRange = range > 0f ? Mathf.RoundToInt(range) : 1;
            Vector2Int origin = owner.Board.WorldToCell(owner.transform.position);
            foreach (Tile tile in owner.Board.GetTiles(origin, cellRange))
            {
                if (tile.OccupantObject == null) continue;
                if (!IsHeroOccupant(tile)) continue;
                if (!hit.Add(tile.OccupantObject)) continue;

                if (tile.OccupantObject.GetComponentInParent<Hero>() is Hero target)
                    owner.ApplyDebuffTo(target, debuffSO);
            }
        }
        finally
        {
            if (owner != null) owner.MovementSuspended = false;
        }
    }
    private static bool IsHeroOccupant(Tile tile)
    {
        return tile.State.Occupant is OccupantKind.MeleeHero or OccupantKind.RangedHero;
    }
    private static async UniTask WaitForAnimationEnd(EnemyBase owner, string stateName, float timeout, CancellationToken token)
    {
        var anim = owner.animator;
        const int layer = 0;

        float elapsed = 0f;
        while (owner != null && !anim.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
        {
            if (owner.IsDead) return;
            elapsed += Time.deltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"StompStun: Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", owner);
                return;
            }
            await UniTask.Yield(token);
        }
        if (owner == null) return;

        var info = anim.GetCurrentAnimatorStateInfo(layer);
        float wait = info.length / Mathf.Max(0.01f, anim.speed);
        await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);
    }
}
