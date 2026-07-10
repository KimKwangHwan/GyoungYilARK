using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public enum EnemyType
{
    Normal,
    Elite,
    Boss,
}
public abstract class EnemyBase : MonoBehaviour,IDamageAble
{
    [SerializeField] protected string enemyKey;
    [SerializeField] protected List<SkillDataSO> skills = new();

    private float[] skillTimers;
    private bool[] skillRunning;
    private CancellationTokenSource skillCts;
    public float Hp { get; protected set; }
    public float MaxHp { get; protected set; }
    public int Defense { get; protected set; }
    public int AttackPower { get; protected set; }
    public float AttackSpeed { get; protected set; }
    public int Range { get; protected set; }
    public float MoveSpeed { get; protected set; }
    public EnemyType Type { get; protected set; }
    public bool IsDie { get; protected set; }
    public Animator animator;

    private float currentMovespeed;
    private bool isAttack;

    protected virtual void Awake()
    {
        LoadStats();
    }

    protected virtual void OnEnable()
    {
        EnemyRegistry.Register(this);
        skillCts = new CancellationTokenSource();
        RunSkillLoop(skillCts.Token).Forget();
        RunAttackLoop(skillCts.Token).Forget();
    }

    protected virtual void OnDisable()
    {
        EnemyRegistry.Unregister(this);
        skillCts?.Cancel();
        skillCts?.Dispose();
        skillCts = null;
    }
    private async UniTask RunSkillLoop(CancellationToken token)
    {
        if (skills == null || skills.Count == 0) return;
        skillTimers = new float[skills.Count];
        skillRunning = new bool[skills.Count];

        while (!IsDie)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] == null || skillRunning[i]) continue;
                skillTimers[i] += Time.deltaTime;
                if (skillTimers[i] >= skills[i].cooldown)
                {
                    skillTimers[i] = 0f;
                    RunSkill(i).Forget();
                }
            }
            await UniTask.Yield(token);
        }
    }

    private async UniTask RunSkill(int index)
    {
        skillRunning[index] = true;
        try { await skills[index].Execute(this); }
        finally { skillRunning[index] = false; }
    }

    // 기본공격 루프. AttackSpeed 를 초당 공격 횟수로 해석 (interval = 1/AttackSpeed).
    private async UniTask RunAttackLoop(CancellationToken token)
    {
        float attackTimer = 0f;
        while (!IsDie)
        {
            float interval = AttackSpeed > 0f ? 1f / AttackSpeed : 1f;
            attackTimer += Time.deltaTime;
            if (attackTimer >= interval)
            {
                attackTimer = 0f;
                Attack();
            }
            await UniTask.Yield(token);
        }
    }

    protected void LoadStats()
    {
        if (string.IsNullOrEmpty(enemyKey)) enemyKey = GetType().Name;

        var enemyTable = DataTableManager.Get<EnemyTable>(DataTableIds.Enemy);
        if (enemyTable == null) return;

        var data = enemyTable.Get(enemyKey);
        if (data == null)
        {
            Debug.LogWarning($"EnemyBase: EnemyTable에서 '{enemyKey}' 데이터를 찾을 수 없음");
            return;
        }
        ApplyData(data);
        LoadSkills(data.Skills);
    }

    private void LoadSkills(string skillIds)
    {
        if (string.IsNullOrEmpty(skillIds)) return;

        foreach (var raw in skillIds.Split(';'))
        {
            var id = raw.Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var so = Resources.Load<SkillDataSO>($"Skills/{id}");
            if (so == null)
            {
                Debug.LogWarning($"EnemyBase: 스킬 '{id}' 로드 실패 (Resources/Skills/{id}). 임포터를 먼저 실행했는지 확인");
                continue;
            }
            if (!skills.Contains(so)) skills.Add(so);
        }
    }

    protected virtual void ApplyData(EnemyTable.Data data)
    {
        AttackPower = data.Attack;
        AttackSpeed = data.AttackSpeed;
        Range = data.Range;
        Defense = data.Defense;
        MaxHp = data.Health;
        Hp = data.Health;
        MoveSpeed = data.MoveSpeed;
        Type = ParseType(data.Type);
        IsDie = false;
    }

    private static EnemyType ParseType(string raw)
    {
        if (!string.IsNullOrEmpty(raw) && System.Enum.TryParse(raw.Trim(), true, out EnemyType type))
            return type;
        return EnemyType.Normal;
    }

    public void TakeDamage(int damage)
    {
        if(IsDie)return;
        int hitDamage = Mathf.Max(1,damage-Defense);
        Hp -= hitDamage;
        if(Hp<=0)Die();

    }

    // 힐 (MaxHp 초과 안 함)
    public void Heal(float amount)
    {
        if (IsDie || amount <= 0f) return;
        Hp = Mathf.Min(Hp + amount, MaxHp);
    }

    // 기본공격: 사거리(Range 칸) 안 가장 가까운 영웅을 AttackPower 로 때림.
    // 대상이 없으면 아무 일도 안 함. (HeroRegistry.Alive 가 비어있으면 안전하게 무시)
    public virtual void Attack()
    {
        if (IsDie) return;

        var target = EnemyTargeting.FindNearest(
            transform.position, Range, HeroRegistry.Alive, h => h.transform.position);
        if (target == null) return;

        target.TakeDamage(AttackPower);
    }

    public virtual void Die()
    {
        if(IsDie)return;
        IsDie= true;
        //대충 죽는거
    }

    // Ai한테 부탁한 예시
    // ===================== 예시용 (실제 로직 아님) =====================
    // 고른 방식: [공유 HP 풀] + [오라(동적)] 보호막
    //  - 공유 HP : 보호막 HP가 하나. 범위 안 누가 맞든 이 HP가 깎이고, 0되면 전원 해제
    //  - 오라     : 맞는 그 순간 내가 보호막 반경 안이면 보호. 들어오면 즉시 보호/나가면 해제

    // 공유 보호막 한 덩어리. 엘리트가 시전하면 하나 만들어져서 모두가 참조만 함.
    public class ShieldInstance
    {
        public float Hp;              // 공유 HP (한 밑바)
        public Vector3 Center;        // 중심(=엘리트 위치). 오라라 계속 갱신 가능
        public float Radius;          // 보호 반경
        private readonly float expireTime;

        public ShieldInstance(float hp, Vector3 center, float radius, float duration)
        {
            Hp = hp;
            Center = center;
            Radius = radius;
            expireTime = Time.time + duration;
        }

        // HP 남아있고 시간 안 지났으면 살아있음
        public bool IsActive => Hp > 0f && Time.time < expireTime;

        // 오라 판정: 이 위치가 지금 반경 안인가?
        public bool Covers(Vector3 pos) => (pos - Center).sqrMagnitude <= Radius * Radius;

        public void Absorb(float dmg) => Hp -= dmg;
    }

    // 예시: 엘리트가 시전한 공유 보호막. 오라라서 위치만 맞으면 아무나 보호받음.
    // (실제로는 시전자나 매니저가 들고 있게 됨 — 여긴 데모라 static)
    public static ShieldInstance ActiveShield;

    // 예시용 데미지 처리. 실제 TakeDamage와 별개로, 위 두 방식이 어떻게 도는지 보여줌.
    public void TestTakeDamage(int damage)
    {
        if (IsDie) return;
        int hitDamage = Mathf.Max(1, damage - Defense);

        // [오라] 맞는 순간 내가 반경 안 && 막이 살아있으면 → [공유풀]이 대신 받음
        if (ActiveShield != null && ActiveShield.IsActive && ActiveShield.Covers(transform.position))
        {
            ActiveShield.Absorb(hitDamage);
            Debug.Log($"[Shield] {name} 이(가) 피해 {hitDamage} → 공유막이 흡수, 공유막 HP {ActiveShield.Hp}");
            return;   // 내 HP는 안 깎임
        }

        // 보호 못 받으면 평소대로 내 HP가 깎임
        Hp -= hitDamage;
        Debug.Log($"[Normal] {name} 이(가) 피해 {hitDamage} → 내 HP {Hp}");
        if (Hp <= 0) Die();
    }
    // ================================================================

}


