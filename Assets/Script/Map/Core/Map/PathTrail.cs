using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class PathTrail : MonoBehaviour
{
    [SerializeField] private TrailRenderer trailPrefab;
    [SerializeField] private TrailPalette palette; //경로 종류별 색상을 담은 공용 자산.
    [SerializeField] private float moveSpeed = 8f; //실제 값은 inspector에서 조절한다.
    [SerializeField] private float trailLift = 0.15f;
    [SerializeField] private float loopGap = 1.2f;

    private readonly List<TrailRun> runs = new();
    private WaveSpawner spawner; //활성화된 스폰 지점 참조.
    private EnemyLanes enemyLanes; //스폰별 갈래 조회용.
    private PathTrailCalc calc; //이번 라운드에 그릴 경로 데이터 계산 담당.
    private TrailRunBuilder builder; //경로 점을 트레일 오브젝트로 만들고 없애는 담당.
    private TrailPlaybackState playback; //반복/대기/1회 재생 상태와 이동 담당.

    // 필요한 컴포넌트 참조와 협력 클래스를 초기화한다.
    private void Awake()
    {
        spawner = GetComponent<WaveSpawner>();
        enemyLanes = GetComponent<EnemyLanes>();
        calc = new PathTrailCalc(spawner, enemyLanes);
        builder = new TrailRunBuilder(trailPrefab, palette, transform, trailLift);
        playback = new TrailPlaybackState(runs, moveSpeed, loopGap);
    }

    // 매 프레임 재생 상태를 진행시킨다.
    private void Update()
    {
        playback.Tick(Time.deltaTime);
    }

    // 포털 갱신 다음 프레임에 최신 활성 경로를 반복 재생한다.
    public async void PlayLoop()
    {
        bool isCanceled = await UniTask.NextFrame(this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (isCanceled || !isActiveAndEnabled) return;

        LoadPoints();
        playback.Begin(loop: true);
    }

    // 모든 활성 경로를 한 번 재생한다.
    public void PlayOnce()
    {
        LoadPoints();
        playback.Begin(loop: false);
    }

    // 모든 Trail의 재생과 대기 상태를 정지한다.
    public void StopTrail()
    {
        playback.Stop();
    }

    // 이번 라운드의 활성 경로로 Trail 목록을 다시 구성한다.
    private void LoadPoints()
    {
        playback.Stop();
        builder.Clear(runs);

        List<TrailPoints> points = calc.CollectRuns();
        builder.Build(points, runs);
    }
}
