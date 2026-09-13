using System;
using System.IO;
using UnityEngine;

// 포트폴리오용 파티클 제한 A/B 비교 계측 도구. F9 하나로 ParticleBudget.LimitEnabled(파티클
// 개수 제한)와 VfxVisibility.Enabled(화면 밖 컷)를 함께 같은 상태로 토글하고 현재 FPS/상태를
// 화면에 텍스트+그래프로 표시한다. 게임 내 "밤"(DayNightData.IsNight) 동안에만 1초마다 CSV에
// 기록한다. 씬/프리팹 수정 없이 RuntimeInitializeOnLoadMethod로 자동 생성되며, 순수 디버그
// 오버레이라 게임플레이 로직에는 영향을 주지 않는다.
public class PerfOverlay : MonoBehaviour
{
    private const float SampleInterval = 0.5f;
    private const float LogInterval = 1f;
    private const int GraphSamples = 60; // SampleInterval 기준 최근 30초
    private const float GraphMaxFps = 70f;

    private float fps;
    private float accumTime;
    private int accumFrames;
    private float worstDeltaThisWindow;
    private float displayedWorstMs;
    private GUIStyle style;

    private readonly float[] fpsHistory = new float[GraphSamples];
    private int historyCount;
    private int historyHead;
    private Texture2D graphTex;
    private bool graphDirty;

    // PerfOverlay는 씬에 배치된 오브젝트가 아니라 부트스트랩으로 생성돼 DI 주입을 받을 수 없다 —
    // MapGame.DayNightData.IsNight를 읽으려고 씬에서 직접 찾아 캐시해둔다.
    private MapGame mapGame;
    private float findGameAccumTime;
    private float logAccumTime;
    private string logPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("PerfOverlay");
        go.AddComponent<PerfOverlay>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        logPath = Path.Combine(Application.persistentDataPath, "particle_limit_perf_log.csv");
        Debug.Log($"[PerfOverlay] 밤에만 기록되는 성능 로그 경로: {logPath}");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            bool next = !ParticleBudget.LimitEnabled;
            ParticleBudget.LimitEnabled = next;
            EnemyParticleBudget.LimitEnabled = next;
            VfxVisibility.Enabled = next;
        }

        float dt = Time.unscaledDeltaTime;
        if (dt > worstDeltaThisWindow) worstDeltaThisWindow = dt;

        accumTime += dt;
        accumFrames++;
        if (accumTime >= SampleInterval)
        {
            fps = accumFrames / accumTime;
            displayedWorstMs = worstDeltaThisWindow * 1000f;
            worstDeltaThisWindow = 0f;
            accumTime = 0f;
            accumFrames = 0;
            PushHistory(fps);
        }

        UpdateNightLog(dt);
    }

    private void PushHistory(float value)
    {
        fpsHistory[historyHead] = value;
        historyHead = (historyHead + 1) % GraphSamples;
        historyCount = Mathf.Min(historyCount + 1, GraphSamples);
        graphDirty = true;
    }

    private void UpdateNightLog(float dt)
    {
        if (mapGame == null)
        {
            findGameAccumTime += dt;
            if (findGameAccumTime < 1f) return;
            findGameAccumTime = 0f;
            mapGame = FindFirstObjectByType<MapGame>();
            if (mapGame == null) return;
        }

        logAccumTime += dt;
        if (logAccumTime < LogInterval) return;
        logAccumTime = 0f;

        if (mapGame.DayNightData == null || !mapGame.DayNightData.IsNight) return;

        AppendLogRow();
    }

    private void AppendLogRow()
    {
        try
        {
            if (!File.Exists(logPath))
                File.AppendAllText(logPath, "Timestamp,FpsAvg,WorstMs,ParticleLimitOn,ActiveEffects\n");

            string row = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{fps:F1},{displayedWorstMs:F1},{ParticleBudget.LimitEnabled},{ParticleBudget.ActiveCount}\n";
            File.AppendAllText(logPath, row);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[PerfOverlay] 로그 기록 실패: {e.Message}");
        }
    }

    private void RebuildGraphTexture()
    {
        if (graphTex == null)
        {
            graphTex = new Texture2D(GraphSamples, 80, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };
        }

        var clear = new Color(0f, 0f, 0f, 0.35f);
        var bar = new Color(0.2f, 0.9f, 0.3f, 0.9f);
        int height = graphTex.height;

        for (int x = 0; x < GraphSamples; x++)
            for (int y = 0; y < height; y++)
                graphTex.SetPixel(x, y, clear);

        for (int i = 0; i < historyCount; i++)
        {
            int idx = (historyHead - historyCount + i + GraphSamples) % GraphSamples;
            float value = fpsHistory[idx];
            int barHeight = Mathf.Clamp(Mathf.RoundToInt(value / GraphMaxFps * height), 0, height);
            for (int y = 0; y < barHeight; y++)
                graphTex.SetPixel(i, y, bar);
        }

        graphTex.Apply();
        graphDirty = false;
    }

    private void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
            };
        }

        string limitState = ParticleBudget.LimitEnabled ? "ON" : "OFF";
        float worstFps = displayedWorstMs > 0f ? 1000f / displayedWorstMs : fps;
        string text =
            $"FPS avg: {fps:F0}   worst: {worstFps:F0} ({displayedWorstMs:F0}ms)\n" +
            $"Particle Limit: {limitState} (F9)";
        GUI.Box(new Rect(10, 10, 560, 60), text, style);

        if (graphDirty) RebuildGraphTexture();
        if (graphTex != null)
            GUI.DrawTexture(new Rect(10, 76, 240, 80), graphTex);
    }
}
