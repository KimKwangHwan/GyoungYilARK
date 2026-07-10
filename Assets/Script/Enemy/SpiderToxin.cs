using UnityEngine;

public class SpiderToxin : EnemyBase
{
    public float hp;
    public float speed;
    public int attack;
    public int def; //테스트용 인스펙터 확인용 스탯들

    protected override void OnEnable()
    {
        IsDie = false;
        base.OnEnable();
    }

    private void Start()
    {
        hp = Hp;
        speed = MoveSpeed;
        attack = AttackPower;
        def = Defense; //테스트용
    }
}
