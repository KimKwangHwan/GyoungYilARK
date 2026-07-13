using UnityEngine;

/// <summary>
/// 테스트용 근접 공격자. MapBoard의 타일 정보만으로 현재 타일과 주변 타일 위의 적(IDamageAble)을 찾아 공격한다.
/// </summary>
/// //임시용 코드이므로 정식으로 유닛 관련 코드가 완성시 해당 구조를 따르며, 해당 파일은 폐기 처리한다.
public class DummyMeleeAttacker : MonoBehaviour
{
    [Tooltip("타일 단위 공격 범위. 0이면 같은 타일의 적만 공격한다.")]
    public float range = 0f;

    [Tooltip("공격 1회 데미지.")]
    public int power = 5;
    [Tooltip("공격 간격(초).")]
    public float attackInterval = 0.5f;
    public bool logAttack = true;

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
        //if (logAttack) Debug.Log($"[Melee] {name} {_targetObject.name} (-{power}, HP {_target.Hp:0.#})", this);
    }

    private bool IsTargetStillInRange()
    {
        if (_targetObject == null) return false;

        Vector2Int origin = board.WorldToCell(transform.position);
        Vector2Int targetCell = board.WorldToCell(_targetObject.transform.position);
        int tileRange = Mathf.Max(0, Mathf.RoundToInt(range));
        if (MapBoard.TileDistance(origin, targetCell) > tileRange) return false;
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

    private void AcquireTargetFromTiles()
    {
        Vector2Int origin = board.WorldToCell(transform.position);

        if (board.TryGetCell(origin, out Tile current) && TryTargetFromTile(current))
            return;

        int tileRange = Mathf.Max(0, Mathf.RoundToInt(range));
        if (tileRange <= 0) return;

        foreach (Tile tile in board.GetTiles(origin, tileRange, false))
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
            //if (logAttack) Debug.Log($"[Melee] {name} target {_targetObject.name} on {tile.Coord}", this);
            return true;
        }

        return false;
    }

    // 공격 대상이 있으면 자신→대상 선을 Game뷰에 그린다(검증용). Update의 조기 return과 무관하게 매 프레임 갱신.
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
        _line.startColor = new Color(1f, 0.35f, 0.2f);
        _line.endColor = new Color(1f, 0.35f, 0.2f);
        _line.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, range));
    }
}
