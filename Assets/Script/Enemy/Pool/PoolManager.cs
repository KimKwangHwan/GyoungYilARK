using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;


public class PoolManager : MonoBehaviour
{
    private IObjectResolver _resolver;
    [Inject] public void Construct(IObjectResolver resolver) => _resolver = resolver;

    private static PoolManager instance;
    public static PoolManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("PoolManager");
                instance = go.AddComponent<PoolManager>();
            }
            return instance;
        }
    }

    // 프리팹 → 그 프리팹 전용 풀
    private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    // 프리팹 → 비활성 인스턴스가 모이는 컨테이너(reparent 회수 기준점)
    private readonly Dictionary<GameObject, Transform> roots = new();
    // 프리팹 → 그 계층에 [Inject] 대상이 있는지(프리팹당 한 번만 리플렉션으로 검사해 캐싱).
    // 없으면 resolver.Instantiate의 전체 자식 계층 재귀 스캔(스켈레톤 본까지 다 훑음)을 건너뛴다 —
    // 시민/이펙트처럼 순수 시각 프리팹인 경우 인스턴스당 스캔 비용이 그대로 낭비였다.
    private readonly Dictionary<GameObject, bool> injectionRequired = new();

    private bool RequiresInjection(GameObject prefab)
    {
        if (injectionRequired.TryGetValue(prefab, out bool cached)) return cached;

        // Transform/Renderer/NavMeshAgent 같은 엔진 내장 컴포넌트는 [Inject] 대상이 될 수 없다.
        // 특히 캐릭터 리그는 본(Transform)만 수십 개라, Component 전체를 훑으면 리플렉션 비용이
        // 그 본 개수만큼 낭비된다 — MonoBehaviour(커스텀 스크립트)만 검사 대상으로 좁힌다.
        bool required = false;
        foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            if (HasInjectMember(behaviour.GetType()))
            {
                required = true;
                break;
            }
        }

        injectionRequired[prefab] = required;
        return required;
    }

    // [Inject]가 부모 클래스(예: EnemyBase)에 선언되고 실제로는 자식 클래스가 붙는 경우가 있어
    // private 멤버까지 잡으려면 상속 체인을 타입별로 직접 걸어야 한다(BindingFlags.NonPublic은
    // 조회한 타입에 "직접 선언된" 멤버만 반환하고 부모의 private 멤버는 안 준다).
    private const BindingFlags InjectMemberFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static bool HasInjectMember(Type type)
    {
        for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            foreach (MethodInfo m in t.GetMethods(InjectMemberFlags))
                if (m.IsDefined(typeof(InjectAttribute), false)) return true;

            foreach (FieldInfo f in t.GetFields(InjectMemberFlags))
                if (f.IsDefined(typeof(InjectAttribute), false)) return true;

            foreach (PropertyInfo p in t.GetProperties(InjectMemberFlags))
                if (p.IsDefined(typeof(InjectAttribute), false)) return true;
        }

        return false;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    // 풀이 비어있어 새로 생성해야 할 때 쓸 위치/회전 - Spawn()이 Get() 호출 직전에 채워 넣는다.
    // createFunc에서 위치를 안 주고 Instantiate하면 NavMeshAgent가 root의 위치(NavMesh 밖일 수 있음)에서
    // 먼저 OnEnable 돼버려 "Failed to create agent because it is not close enough to the NavMesh" 경고가 뜬다.
    // Instantiate(prefab, position, rotation, parent) 오버로드는 Awake/OnEnable 전에 위치를 확정하므로 이걸 막는다.
    private Vector3 pendingSpawnPos;
    private Quaternion pendingSpawnRot;

    private ObjectPool<GameObject> GetPool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out var pool)) return pool;

        var root = new GameObject($"Pool_{prefab.name}").transform;
        root.SetParent(transform);
        roots[prefab] = root;

        bool needsInjection = RequiresInjection(prefab);

        pool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                // resolver가 있고 실제로 [Inject] 대상이 있을 때만 VContainer로 생성 → 스폰된 적의
                // [Inject]가 채워진다. 주입 대상이 없으면(순수 시각 프리팹) 전체 계층 재귀 스캔을
                // 건너뛰고 일반 Instantiate로 생성해 스폰 비용을 줄인다.
                var go = needsInjection && _resolver != null
                    ? _resolver.Instantiate(prefab, pendingSpawnPos, pendingSpawnRot, root)
                    : Instantiate(prefab, pendingSpawnPos, pendingSpawnRot, root);
                var po = go.GetComponent<PooledObject>();
                if (po == null) po = go.AddComponent<PooledObject>();
                po.Init(prefab, this); // 자기 풀을 넘겨 회수 시 쓰게(AddComponent는 주입 안 되므로 직접 전달)
                go.SetActive(false);
                return go;
            },
            actionOnGet: null,                         // 활성화/위치 세팅은 Spawn에서
            actionOnRelease: go =>
            {
                if (go == null) return;
                go.transform.SetParent(root);          // 부모(적 등)에 붙었던 이펙트도 풀 루트로 회수
                go.SetActive(false);
            },
            actionOnDestroy: go => { if (go != null) Destroy(go); },
            collectionCheck: true,                     // 중복 Release 감지(에디터 안전망)
            defaultCapacity: 8,
            maxSize: 256
        );
        pools[prefab] = pool;
        return pool;
    }

    public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (prefab == null) return null;

        var pool = GetPool(prefab);
        pendingSpawnPos = pos;
        pendingSpawnRot = rot;
        var go = pool.Get();
        var t = go.transform;
        if (parent != null) t.SetParent(parent);
        t.SetPositionAndRotation(pos, rot);

        go.SetActive(true);
        go.GetComponent<PooledObject>().OnSpawned();   // 활성화 후 파티클 리셋/상태 초기화
        return go;
    }

    // Spawn + EnemyParticleBudget 게이팅. 예산이 없으면 스폰 자체를 건너뛰고 null을 반환한다
    // (연출용 이펙트라 스킵해도 안전 — 호출부는 Hero.SpawnEffect와 같은 방식으로 null을 다룬다).
    // 해제는 Despawn(GameObject) 한 곳에서만 이뤄지므로 이 이펙트의 Despawn 호출부는 손댈 필요 없다.
    public GameObject SpawnBudgeted(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (!EnemyParticleBudget.TryAcquire()) return null;

        GameObject go = Spawn(prefab, pos, rot, parent);
        if (go == null) { EnemyParticleBudget.Release(); return null; }

        var po = go.GetComponent<PooledObject>();
        if (po != null) po.Budgeted = true;
        return go;
    }

    public void Despawn(GameObject go)
    {
        if (go == null) return;

        var po = go.GetComponent<PooledObject>();
        if (po == null) { Destroy(go); return; }       // 풀 출신이 아니면 그냥 파괴(안전망)
        if (po.IsReleased) return;                     // 중복 Despawn 가드

        // 풀 반납(SetActive(false)→OnDisable)보다 먼저 지워야, 그 사이 발생할 수 있는
        // 다른 해제 경로(PooledObject.OnDestroy 안전망)와 중복 해제되지 않는다.
        if (po.Budgeted) { po.Budgeted = false; EnemyParticleBudget.Release(); }

        po.MarkReleased();
        GetPool(po.SourcePrefab).Release(go);
    }
    public void Despawn(GameObject go, float delay,bool scaleCheck = true)
    {
        if (go == null) return;
        if (delay <= 0f) { Despawn(go); return; }

        var po = go.GetComponent<PooledObject>();
        if (po == null) { Destroy(go, delay); return; }
        po.ScheduleDespawn(delay,scaleCheck);

        
    }
}