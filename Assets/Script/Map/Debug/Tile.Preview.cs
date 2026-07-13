using UnityEngine;

// 저작 유닛 동기화 (에디트 · 플레이 · 빌드 공통).
// Tile.UnitPrefab이 바뀌면 그 타일 위 인스턴스를 다시 만든다.
//  - [ExecuteAlways] + Update 변경감지 → 에디트/플레이 양쪽에서 반영(Update는 두 모드 다 돈다).
//  - 에디트: 미리보기(HideAndDontSave · 타일 자식 · 씬 저장 안 함).
//  - 플레이/빌드: 실제 유닛(SetOccupant로 점유 등록 · 타일 자식 아님).
// 변경이 있을 때만 재생성하므로 평소 프레임 비용은 참조 비교 1회뿐이다.
[ExecuteAlways]
public partial class Tile
{
    private const string SpawnName = "__AuthoredUnit";
    private GameObject _spawned;        // 이 타일이 만든 인스턴스(직렬화 안 함)
    private GameObject _spawnedSource;  // 그 인스턴스의 원본 프리팹 — 변경 감지 기준

    // 매 틱 "프리팹이 바뀌었나"만 본다. 안 바뀌었으면 즉시 return(비용 거의 0).
    private void Update()
    {
        if (_spawnedSource == UnitPrefab) return;
        Reconcile();
    }

    private void Reconcile()
    {
        CleanStray();       // 재컴파일로 참조 잃은 떠돌이 정리(최대 1개 유지)
        DestroySpawned();   // 이전 인스턴스 제거

        _spawnedSource = UnitPrefab;
        if (UnitPrefab == null)
        {
            if (Application.isPlaying && OccupantObject != null) ClearOccupant();
            return;
        }

        _spawned = Instantiate(UnitPrefab);
        _spawned.name = SpawnName;
        _spawned.transform.position = TopCenter();

        if (Application.isPlaying)
        {
            SetOccupant(_spawned, UnitKind);                 // 실제 점유(타일 자식 아님)
        }
        else
        {
            _spawned.transform.SetParent(transform, true);   // 에디트: 정리 편의로 자식
            _spawned.hideFlags = HideFlags.HideAndDontSave;  // 씬에 저장하지 않음
        }
    }

    private void DestroySpawned()
    {
        if (_spawned == null) return;
        if (Application.isPlaying) Destroy(_spawned); else DestroyImmediate(_spawned);
        _spawned = null;
    }

    private void CleanStray()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c == null || c.name != SpawnName || c.gameObject == _spawned) continue;
            if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
        }
    }

    // 타일 윗면 월드 좌표. 런타임 _topY 캐시에 의존하지 않도록 렌더러 바운드로 직접 구한다(생성 인스턴스 제외).
    private Vector3 TopCenter()
    {
        bool has = false;
        Bounds b = default;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r == null) continue;
            if (_spawned != null && r.transform.IsChildOf(_spawned.transform)) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        float topY = has ? b.max.y : transform.position.y;
        return new Vector3(transform.position.x, topY, transform.position.z);
    }
}
