using UnityEngine;
using Cysharp.Threading.Tasks;

public enum EffectType
{
    None,
    Dash,
    Teleport,
    Heal,
    Shield,
    Summon,
    Split,
    Stealth,
}

public abstract class SkillDataSO : ScriptableObject
{
    public string skillName; 
    public float cooldown;
    public float duration;
    public float range;
    public float tickInterval;

    public abstract UniTask Execute(EnemyBase owner);
}
