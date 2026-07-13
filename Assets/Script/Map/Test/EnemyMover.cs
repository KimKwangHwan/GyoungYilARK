using System.Collections.Generic;
using UnityEngine;

 
public class EnemyMover : MonoBehaviour
{
    public float speed = 2f;
    public float yOffset = 0.5f;
    public bool destroyOnArrive = true;
    public MapBoard board;
    //Enemy측 이동 경로 방식에 따라 해당 파일은 대체 될수 있음.
    //적이 이동할 경로 List에 해당 타일을 넣고 경로를 SetPath로 주면, Enemy가 자동으로 이동하며 도착 시 파괴된다.
    private readonly List<Vector3> _path = new();
    private int _index;
    private bool _active;
    private Vector2Int _lastCoord = new(int.MinValue, int.MinValue); // 직전 칸(로컬 캐시) — 바뀔 때만 보드 갱신

    //월드 경로로 이동한다. 칸 판정은 매 프레임 현재 위치를 WorldToCell로 역산(경계 0.5 전환).
   
    public void SetPath(IReadOnlyList<Vector3> worldPath, float moveSpeed, float surfaceOffset, IReadOnlyList<Tile> pathTiles = null)
    {
        speed = moveSpeed;
        yOffset = surfaceOffset;

        _path.Clear();
        if (worldPath != null)
            foreach (Vector3 p in worldPath) _path.Add(p + Vector3.up * yOffset);

        _index = 0;
        _active = _path.Count > 0;
        if (_active)
        {
            transform.position = _path[0];
            if (board != null)
            {
                _lastCoord = board.WorldToCell(transform.position);
                board.MoveEnemy(gameObject, transform.position); // 시작 칸(스폰) 등록
            }
        }
    }

    private void Update()
    {
        if (!_active) return;

        // 저지 중이면 그 자리에 정지 → 진입 경계에서 멈춰 근접유닛(타일 중앙)과 겹치지 않는다.
        bool blocked = board != null && board.IsBlocked(gameObject);
        if (!blocked)
        {
            Vector3 target = _path[_index];
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            // 진행 방향 바라보기
            Vector3 flat = target - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 12f * Time.deltaTime);
        }

        // 칸 소속은 현재 위치 기준(중심이 가장 가까운 타일 = 경계 0.5 전환).
        // 로컬에서 좌표만 싸게 계산하고, 칸이 실제로 바뀐 프레임에만 보드를 갱신한다(dict 조회·이벤트 최소화).
        if (board != null)
        {
            Vector2Int now = board.WorldToCell(transform.position);
            if (now != _lastCoord)
            {
                _lastCoord = now;
                board.MoveEnemy(gameObject, transform.position);
            }
        }

        if (Vector3.SqrMagnitude(transform.position - _path[_index]) > 0.0004f) return;

        // 이번 waypoint 도달 → 다음 목표로 (칸 등록은 위에서 매 프레임 처리).
        _index++;
        if (_index < _path.Count) return;

        _active = false;
        if (destroyOnArrive) Destroy(gameObject);
    }

    // 도착 파괴·웨이브 정리 등 어떤 경로로 사라지든 현재 칸에서 빠지도록 한 곳에서 해제한다.
    private void OnDestroy()
    {
        if (board != null) board.RemoveEnemy(gameObject);
    }
}
