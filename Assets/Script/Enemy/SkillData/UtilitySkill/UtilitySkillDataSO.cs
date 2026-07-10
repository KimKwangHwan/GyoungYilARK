using UnityEngine;

// 유틸리티 스킬 공통 베이스 (Dash, Summon 등이 상속). 동작은 각 하위 클래스에서 구현.
public abstract class UtilitySkillDataSO : SkillDataSO
{
    public float value;
}
