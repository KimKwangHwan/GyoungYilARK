using UnityEngine;


//원거리용 아군 유닛 임시용.
public class DummyRangedAttacker : MonoBehaviour
{
    [Tooltip("타일 단위 공격 범위. 원점(자기 타일)은 제외하고 주변만 훑는다.")]
    public float range = 2f;

    public int AttackRange => Mathf.Max(0, Mathf.RoundToInt(range));
    [Tooltip("공격 1회 데미지.")]
    public int power = 4;
    [Tooltip("공격 간격(초).")]
    public float attackInterval = 0.7f;
    public bool logAttack = false;

    public MapBoard board;

    [Tooltip("공격 선 표시용 머티리얼(Game뷰). 비우면 선을 그리지 않음. Shader.Find 금지라 주입.")]
    public Material lineMaterial;

    private GameObject _targetObject;
    private IDamageAble _target;
    private float _timer;
    private LineRenderer _line;

    private void Update()
    {
        if (board == null) return;

        if (_targetObject == null || _target == null || !IsTargetStillInRange())
            ClearTarget();

        if (_targetObject == null || _target == null)
            AcquireTargetFromTiles();

        if (_targetObject == null || _target == null)
        {
            _timer = 0f;
            return;
        }

        _timer += Time.deltaTime;
        if (_timer < attackInterval) return;
        _timer = 0f;

        _target.TakeDamage(power);
        if (logAttack) Debug.Log($"[Ranged] {name} → {_targetObject.name} (-{power}, HP {_target.Hp:0.#})", this);
    }

    private bool IsTargetStillInRange()
    {
        if (_targetObject == null) return false;

        Vector2Int origin = board.WorldToCell(transform.position);
        Vector2Int targetCell = board.WorldToCell(_targetObject.transform.position);
        if (MapBoard.TileDistance(origin, targetCell) > AttackRange) return false;
        if (!board.TryGetCell(targetCell, out Tile tile)) return false;

        foreach (GameObject enemy in tile.Enemies)
            if (enemy == _targetObject)
                return true;

        return false;
    }

    private void ClearTarget()
    {
        _targetObject = null;
        _target = null;
    }

    // 저지 개념이 없으므로 자기 타일 우선순위 없이 사거리 안의 적만 훑는다(원점 제외).
    private void AcquireTargetFromTiles()
    {
        if (AttackRange <= 0) return;

        Vector2Int origin = board.WorldToCell(transform.position);
        foreach (Tile tile in board.GetTiles(origin, AttackRange, false))
        {
            if (tile.Coord == origin) continue;
            if (TryTargetFromTile(tile)) return;
        }
    }

    private bool TryTargetFromTile(Tile tile)
    {
        foreach (GameObject enemy in tile.Enemies)
        {
            if (enemy == null) continue;
            if (enemy.GetComponentInParent<IDamageAble>() is not IDamageAble damageable) continue;

            _targetObject = enemy;
            _target = damageable;
            if (logAttack) Debug.Log($"[Ranged] {name} target {_targetObject.name} on {tile.Coord}", this);
            return true;
        }

        return false;
    }

    // 대상이 있으면 자신→대상 선을 Game뷰에 그린다(검증용). 원거리는 파란 선.
    private void LateUpdate()
    {
        if (_line == null)
        {
            SetupLine();
        }
        if (_line == null)
        {
            return;
        }

        bool show = _targetObject != null && _target != null;
        _line.enabled = show;
        if (show)
        {
            _line.SetPosition(0, transform.position);
            _line.SetPosition(1, _targetObject.transform.position);
        }
    }

    private void SetupLine()
    {
        if (lineMaterial == null)
        {
            return;
        }

        _line = GetComponent<LineRenderer>();
        if (_line == null)
        {
            _line = gameObject.AddComponent<LineRenderer>();
        }
        _line.material = lineMaterial;
        _line.positionCount = 2;
        _line.widthMultiplier = 0.05f;
        _line.numCapVertices = 2;
        _line.startColor = new Color(0.3f, 0.6f, 1f);
        _line.endColor = new Color(0.3f, 0.6f, 1f);
        _line.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, range));
    }
}
