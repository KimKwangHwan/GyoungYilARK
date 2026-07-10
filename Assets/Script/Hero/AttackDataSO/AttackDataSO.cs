using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

[CreateAssetMenu(fileName = "AttackDataSO", menuName = "HeroAttack/AttackDataSO")]
public abstract class AttackDataSO : ScriptableObject
{
    public int range = 3;
    public float attackSpeed = 1f;
    public AttackType attackType = AttackType.Single;
    [SerializeField] private string animTrigger = "Attack";
    private int? animHashCache;

    protected int AnimHash => (animHashCache ??= Animator.StringToHash(animTrigger));

    public virtual bool CanExecute(AttackContext ctx)
    {
        return true;
    }

    public abstract UniTask Execute(AttackContext ctx, CancellationToken ct);
}
public enum AttackType
{
    Single,
    Multiple,
}