using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    private GameObject sourcePrefab;
    private ParticleSystem[] particles;
    private CancellationTokenSource dcts;
    private bool released;
    private PoolManager pool; // 자기를 만든 풀(createFunc에서 주입) — 지연 회수에 사용
    public GameObject SourcePrefab => sourcePrefab;
    public bool IsReleased => released;

    // PoolManager.SpawnBudgeted로 스폰됐으면 true — EnemyParticleBudget 슬롯을 쥐고 있다는 표시.
    // PoolManager.Despawn이 정상적으로 회수하면서 false로 지운다.
    public bool Budgeted;

    // createFunc에서 1회 호출. 캐싱은 여기서 한 번만.
    public void Init(GameObject prefab, PoolManager owner)
    {
        sourcePrefab = prefab;
        pool = owner;
        particles = GetComponentsInChildren<ParticleSystem>(true);
    }
    void OnEnable()
    {
        dcts = new CancellationTokenSource();
    }

    // Spawn 직후(활성화된 뒤) 호출: 상태 초기화 + 파티클 재생.
    public void OnSpawned()
    {
        released = false;
        dcts.Cancel();
        dcts.Dispose();
        dcts = new CancellationTokenSource();
        if (particles != null)
        {
            foreach (var ps in particles)
            {
                if (ps == null) continue;
                ps.Clear(true);
                ps.Play(true);
            }
        }
    }
    public void MarkReleased()
    {
        released = true;
        dcts.Cancel();
        dcts.Dispose();
        dcts = new CancellationTokenSource();
    }

    // delay초 뒤 자기 자신을 풀로 회수 (기존 Destroy(go, t) 대체).
    public void ScheduleDespawn(float delay,bool scaleCheck=true)
    {
        dcts.Cancel();
        dcts.Dispose();
        dcts = new CancellationTokenSource();
        DespawnAfter(delay,scaleCheck).Forget();
    }

    private async UniTask DespawnAfter(float delay,bool scaleCheck = true)
    {
        var token = dcts.Token;
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay),ignoreTimeScale:!scaleCheck,cancellationToken : token);   
        }
        catch(OperationCanceledException)
        {
            return;
        }
        pool.Despawn(gameObject);
    }

    // 누수 안전망: Despawn을 거치지 않고(부모가 씬 언로드·직접 Destroy 등으로 함께 파괴되는 경우)
    // 이 오브젝트가 사라지면, Budgeted가 아직 true로 남아있을 수 있다 — 예산 슬롯을 회수한다.
    // 정상 경로(PoolManager.Despawn)는 이 시점 전에 이미 Budgeted를 false로 지워두므로 중복 해제되지 않는다.
    private void OnDestroy()
    {
        if (Budgeted)
        {
            Budgeted = false;
            EnemyParticleBudget.Release();
        }
    }
}
