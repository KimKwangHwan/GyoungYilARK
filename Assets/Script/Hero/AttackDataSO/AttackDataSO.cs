using UnityEngine;

public enum AnimSelectMode { Sequential, Random }

[CreateAssetMenu(fileName = "AttackData", menuName = "HeroAttack/AttackData")]
public class AttackDataSO : ScriptableObject
{
    public int range = 3;
    public AttackType attackType = AttackType.Single;

    public string[] animTriggers = { "Attack" };
    public AnimSelectMode selectMode = AnimSelectMode.Sequential;
}

public enum AttackType
{
    Single,
    Multiple,
}
