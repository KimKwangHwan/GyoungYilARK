using System;
using UnityEngine;

public class MapPanel : MonoBehaviour
{
    private const float PanelWidth = 230f;

    private PanelData data;

    public bool HasPointer { get; private set; }

    public event Action PathClicked;
    public event Action PathToggle;
    public event Action EnemyClicked;
    public event Action WaveClicked;
    public event Action EnemyCleared;
    public event Action UnitCleared;
    public event Action ScanClicked;
    public event Action RemoveClicked;
    public event Action ModeCleared;
    public event Action<int> UnitClicked;

    public void SetData(PanelData value)
    {
        data = value;
    }

    private void OnGUI()
    {
        if (data == null)
        {
            HasPointer = false;
            return;
        }

        Rect info = new(10f, 10f, PanelWidth, 300f);
        Rect test = new(Screen.width - PanelWidth - 10f, 10f, PanelWidth, 560f);
        HasPointer = info.Contains(Event.current.mousePosition) || test.Contains(Event.current.mousePosition);

        DrawInfo(info);
        DrawTest(test);
    }

    private void DrawInfo(Rect rect)
    {
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label("맵 정보");
        GUILayout.Label(data.Status);

        if (!string.IsNullOrEmpty(data.TileText))
        {
            GUILayout.Space(4f);
            GUILayout.Label(data.TileText);
        }

        GUILayout.EndArea();
    }

    private void DrawTest(Rect rect)
    {
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label("맵 테스트");
        DrawPath();
        DrawEnemy();
        DrawUnits();

        GUILayout.Space(6f);
        if (GUILayout.Button("보드 다시 스캔"))
        {
            ScanClicked?.Invoke();
        }

        GUILayout.EndArea();
    }

    private void DrawPath()
    {
        GUILayout.Label($"경로 표시: {(data.ShowPath ? "ON" : "OFF")}");
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("경로 재계산"))
        {
            PathClicked?.Invoke();
        }

        if (GUILayout.Button(data.ShowPath ? "경로 끄기" : "경로 켜기"))
        {
            PathToggle?.Invoke();
        }

        GUILayout.EndHorizontal();
    }

    private void DrawEnemy()
    {
        GUILayout.Space(6f);
        GUILayout.Label("적");

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("적 생성"))
        {
            EnemyClicked?.Invoke();
        }

        if (GUILayout.Button($"웨이브 {data.WaveCount}"))
        {
            WaveClicked?.Invoke();
        }

        GUILayout.EndHorizontal();

        if (GUILayout.Button("적 전멸"))
        {
            EnemyCleared?.Invoke();
        }
    }

    private void DrawUnits()
    {
        GUILayout.Space(6f);
        GUILayout.Label($"배치 모드: {data.Mode}");

        for (int i = 0; i < data.Units.Length; i++)
        {
            string mark = data.UnitIndex == i ? "> " : "";
            if (GUILayout.Button($"{mark}{data.Units[i]}"))
            {
                UnitClicked?.Invoke(i);
            }
        }

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("제거"))
        {
            RemoveClicked?.Invoke();
        }

        if (GUILayout.Button("끄기"))
        {
            ModeCleared?.Invoke();
        }

        GUILayout.EndHorizontal();

        if (GUILayout.Button("배치 전부 제거"))
        {
            UnitCleared?.Invoke();
        }
    }
}
