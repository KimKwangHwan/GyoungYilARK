using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

// 각 모듈의 월드 사각형은 그 모듈 보드의 WorldBounds에서 얻는다.
public class FogController : MonoBehaviour
{
    [SerializeField] private MapRegistry registry;
    [SerializeField, Min(0.05f)] private float openDuration = 4f;   // 안개가 완전히 걷히는 시간(초)

    [Header("Debug/Test")]
    [Tooltip("끄면 안개 셰이더 효과를 완전히 없앤다(그리기 재료를 0개로 만듦). 테스트용 스위치.")]
    [SerializeField] private bool fogEnabled = true;

    public static event Action<int> RevealDone;

    [Header("Fog Edge")]
    [FormerlySerializedAs("edgeCells")]
    [Tooltip("Changes only the maximum fog reach outside module bounds.")]
    [SerializeField, Range(0f, 8f)] private float coverMarginCells = 1.5f;

    private const int MaxAreas = 8;
    private static readonly int AreasId = Shader.PropertyToID("_FogAreas");
    private static readonly int OpensId = Shader.PropertyToID("_FogOpens");
    private static readonly int CountId = Shader.PropertyToID("_FogCount");
    private static readonly int CoverMarginId = Shader.PropertyToID("_FogCoverMargin");

    private readonly Vector4[] _areas = new Vector4[MaxAreas];
    private readonly float[] _opens = new float[MaxAreas];
    private readonly bool[] _revealed = new bool[MaxAreas];
    private readonly List<ModuleLogic> _modules = new();
    private int _count;
    private float _cellSize;

    private void Start()
    {
        BuildAreas();
        ApplyEdgeMargin();
        ApplyEnabledState();
        BindModules();
        RevealUnlocked();
    }

    private void OnValidate()
    {
        ApplyEdgeMargin();
        ApplyEnabledState();
    }

    // 등록된 모듈마다 보드 경계를 사각형(xy=중심XZ, zw=반크기XZ)으로 담는다.
    private void BuildAreas()
    {
        foreach (ModuleLogic module in registry.AllModules.Values)
        {
            if (_count >= MaxAreas)
            {
                break;
            }

            MapBoard board = module.GetComponent<MapBoard>();
            if (board == null)
            {
                continue;
            }
            if (board.CellCount == 0)
            {
                board.Build();
            }
            if (_cellSize <= 0f)
            {
                _cellSize = board.CellSize;
            }

            _areas[_count] = ApplyModuleBackground(module, MeasureCellArea(board));

            _opens[_count] = 0f;
            _revealed[_count] = false;

            _modules.Add(module);
            _count++;
        }
    }

    // 이 모듈 자식 중 FogBackground가 붙은 장식 배경이 있으면 그 Renderer 범위까지 사각형을 넓혀서 합친다
    // (같은 open 값을 쓰게 되어 그 모듈이 해금될 때 배경도 같이 열린다). 수동 배선이 필요 없다.
    private static Vector4 ApplyModuleBackground(ModuleLogic module, Vector4 area)
    {
        foreach (FogBackground background in module.GetComponentsInChildren<FogBackground>())
        {
            Bounds bounds = background.GetComponent<Renderer>().bounds;
            area = UnionRect(area, new Vector4(bounds.center.x, bounds.center.z, bounds.extents.x, bounds.extents.z));
        }
        return area;
    }

    // 두 사각형(중심XZ, 반크기XZ)을 감싸는 가장 작은 사각형을 만든다.
    private static Vector4 UnionRect(Vector4 a, Vector4 b)
    {
        float minX = Mathf.Min(a.x - a.z, b.x - b.z);
        float maxX = Mathf.Max(a.x + a.z, b.x + b.z);
        float minZ = Mathf.Min(a.y - a.w, b.y - b.w);
        float maxZ = Mathf.Max(a.y + a.w, b.y + b.w);
        return new Vector4((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f, (maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);
    }

    // 점유 셀 중심들의 격자 정렬 사각형(±반 칸). 렌더러 바운드가 아니라 칸에만 의존한다.
    private Vector4 MeasureCellArea(MapBoard board)
    {
        bool has = false;
        float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;
        foreach (Tile tile in board.Cells.Values)
        {
            Vector3 top = tile.WorldTop;
            if (!has)
            {
                minX = maxX = top.x;
                minZ = maxZ = top.z;
                has = true;
            }
            else
            {
                if (top.x < minX) { minX = top.x; }
                if (top.x > maxX) { maxX = top.x; }
                if (top.z < minZ) { minZ = top.z; }
                if (top.z > maxZ) { maxZ = top.z; }
            }
        }

        float half = board.CellSize * 0.5f;
        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;
        float extentX = (maxX - minX) * 0.5f + half;
        float extentZ = (maxZ - minZ) * 0.5f + half;
        return new Vector4(centerX, centerZ, extentX, extentZ);
    }

    // 모듈이 이동된 뒤(해금 시 재배치 등) 캐시된 안개 영역을 그 모듈의 현재 위치로 다시 재고 반영한다.
    public void RefreshArea(ModuleLogic module)
    {
        int index = _modules.IndexOf(module);
        if (index < 0) return;

        MapBoard board = module.GetComponent<MapBoard>();
        _areas[index] = ApplyModuleBackground(module, MeasureCellArea(board));
        ApplyAreas();
    }

    // 포토모드 캡처가 캐릭터만 깨끗이 뽑도록 잠시 안개 셰이더 효과를 껐다 켠다.
    // fogEnabled 스위치와 같은 메커니즘 - 그릴 안개 영역 수를 0으로 만들어 없앤다.
    public void SetFogVisible(bool visible)
    {
        Shader.SetGlobalInt(CountId, visible ? _count : 0);
        if (visible) ApplyOpenState();
    }

    private void RevealUnlocked()
    {
        for (int i = 0; i < _modules.Count; i++)
        {
            if (_modules[i].IsUnlocked)
            {
                _revealed[i] = true;
                OpenStartArea(i);
            }
        }
    }

    // 새 게임이면 시작 지역을 연출로 열고, 이어하기면 연출 없이 즉시 연다.
    private void OpenStartArea(int index)
    {
        if (SelectedSaveSlot.IsNewGame)
        {
            RevealArea(index).Forget();
            return;
        }

        OpenArea(index);
    }

    private void BindModules()
    {
        foreach (ModuleLogic module in _modules)
        {
            module.OnStateChanged += ModuleChanged;
            module.OnUnlocked += ModuleUnlocked;
        }
    }

    // 개방된 모듈마다 한 번만 구멍을 연다. 세이브 복원으로 열린 지역은 연출 없이 즉시 걷는다.
    private void ModuleChanged(ModuleState state)
    {
        for (int index = 0; index < _modules.Count; index++)
        {
            if (_revealed[index])
            {
                continue;
            }
            if (!_modules[index].IsUnlocked)
            {
                continue;
            }

            _revealed[index] = true;
            OpenArea(index);
        }
    }

    // 게임 중 새로 해금된 지역만 안개가 걷히는 연출로 연다.
    private void ModuleUnlocked(ModuleLogic module)
    {
        int index = _modules.IndexOf(module);
        if (index < 0) return;

        _revealed[index] = true;
        RevealArea(index).Forget();
    }

    // 지정한 지역의 안개를 연출 없이 즉시 다 걷어낸다 (완료 알림도 보내지 않는다).
    private void OpenArea(int index)
    {
        _opens[index] = 1f;
        ApplyOpenState();
    }

    private async UniTaskVoid RevealArea(int index)
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();
        float amount = _opens[index];
        while (amount < 1f)
        {
            amount += Time.deltaTime / openDuration;
            if (amount > 1f)
            {
                amount = 1f;
            }
            _opens[index] = amount;
            ApplyOpenState();
            await UniTask.Yield(token);
        }

        NotifyDone(index);
    }

    private void NotifyDone(int index)
    {
        int moduleId = _modules[index].ModuleId; // 안개가 걷힌 지역 번호를 가져온다.
        RevealDone?.Invoke(moduleId); // 해당 지역의 안개 제거 완료를 알린다.
    }

    private void ApplyAreas()
    {
        Shader.SetGlobalVectorArray(AreasId, _areas);
        Shader.SetGlobalFloatArray(OpensId, _opens);
        Shader.SetGlobalInt(CountId, _count);
        Shader.SetGlobalFloat(CoverMarginId, coverMarginCells * _cellSize);
    }

    // 스위치가 꺼져 있으면 그릴 안개 재료를 0개로 만들어 효과를 완전히 없앤다.
    private void ApplyEnabledState()
    {
        if (fogEnabled)
        {
            ApplyAreas();
        }
        else
        {
            Shader.SetGlobalInt(CountId, 0);
        }
    }

    private void ApplyEdgeMargin()
    {
        if (_cellSize <= 0f)
        {
            return;
        }
        Shader.SetGlobalFloat(CoverMarginId, coverMarginCells * _cellSize);
    }

    private void ApplyOpenState()
    {
        Shader.SetGlobalFloatArray(OpensId, _opens);
    }

    private void OnDestroy()
    {
        foreach (ModuleLogic module in _modules)
        {
            if (module != null)
            {
                module.OnStateChanged -= ModuleChanged;
                module.OnUnlocked -= ModuleUnlocked;
            }
        }
    }
}
