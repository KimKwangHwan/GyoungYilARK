using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

// 담당들을 만들어 MapCommand와 MapView에 넣어준다. 조립만 하고 게임 로직은 갖지 않는다.
// Start에서 하는 이유: MapGame의 [Inject] Construct가 Awake 단계에 끝나므로 그 뒤라야 Placer가 채워져 있다.
public class MapAssemble : MonoBehaviour
{
    [SerializeField] private MapGame mapGame;
    [SerializeField] private MapRegistry registry;
    [SerializeField] private MapCommand command;
    [SerializeField] private MapView view;
    [SerializeField] private PlacePalette palette;
    [SerializeField] private ExpandEvent expand;
    [SerializeField] private float dragPixels = 8f;
    [SerializeField] private float placeYOffset = 0f;
    // 저장된 영웅 배치를 UnitPlacer.TryPlace()로 그대로 되돌릴 때 쓰는 배치 높이 조회 통로 (로드 복원 전용)
    public float PlaceYOffset => placeYOffset;
    [Range(0f, 1f)]
    [Tooltip("배치 미리보기의 진하기. 낮출수록 투명해진다.")]
    [SerializeField] private float ghostAlpha = 0.45f;
    [SerializeField] private TilePaintView tilePaintView;
    [SerializeField] private RangeInput rangeInput;
    [SerializeField] private HeroCombineManager combineManager;
    [Tooltip("영웅 배치 성공 시 재생할 EnemySoundManager 키. 비워두면 재생하지 않음")]
    [SerializeField] private string placeSoundKey;
    [Tooltip("영웅 회수(필드에서 제거) 성공 시 재생할 EnemySoundManager 키. 비워두면 재생하지 않음")]
    [SerializeField] private string removeSoundKey;
    [SerializeField] private DesertZone desertZone;
    [SerializeField] private PlayerSkillPanel playerSkillPanel;
    [SerializeField] private GimmickPopup gimmickPopup;

    private List<EnemyLanes> laneModules;
    private TrailStateController trailState;
    private PlaceGhost ghost;
    private HeroSkillCastController skillCast;
    private PlayerSkillCastController playerSkillCast;
    private DesertZoneEffect desertZoneEffect;
    private List<IceZoneEffect> iceZoneEffects;
    private List<IceSnowfall> iceSnowfalls;
    private CampfireLightController campfireLights;
    private MapBoard desertBoard;
    // 씬 로딩 중 게임을 끄면 Start가 끝나기 전에 OnDestroy가 불릴 수 있어, 이때 아래 필드들이
    // 아직 null이라 OnDestroy가 터진다. 이 플래그로 Start 완료 여부를 확인하고 조기 종료한다.
    private bool started;

    [Inject]
    private void Construct(TrailStateController trailState)
    {
        this.trailState = trailState;
    }

    private void Start()
    {
        List<MapBoard> boards;
        CollectModuleComponents(out boards, out laneModules);
        BuildCampfires(boards);

        palette.Bind(mapGame.HeroRoster);

        HoveredTileData hoverData = new HoveredTileData();
        PointerPick pointerPick = new PointerPick(boards, hoverData);
        PlaceFinder finder = new PlaceFinder(pointerPick, palette, placeYOffset);

        desertBoard = desertZone.GetComponent<MapBoard>();
        WindShelterData shelterData = new WindShelterCalc().BuildData(desertBoard.Cells);
        WindwallData windwallData = new WindwallCalc().BuildData(desertBoard.Cells, desertZone.WindwallReach);
        desertZone.SetWindwall(windwallData);
        WindPreview windPreview = new WindPreview(
            desertBoard,
            desertZone.transform,
            desertZone.ArrowSize,
            desertZone.ArrowHeight,
            desertZone.ArrowColor,
            desertZone.ArrowSlideCells,
            desertZone.ArrowSlidePeriod);
        DesertLineEffect lineEffect = new DesertLineEffect(
            desertBoard,
            Resources.Load<GameObject>("ZoneEffectPrefab/DesertWeakVFX"));
        desertZoneEffect = new DesertZoneEffect(
            desertZone,
            desertBoard,
            shelterData,
            windwallData,
            mapGame.Units,
            windPreview,
            lineEffect,
            mapGame.Rule);

        DayNightBuildRule dayNightRule = new DayNightBuildRule();
        dayNightRule.rule = mapGame.Rule;
        palette.dayNightRule = dayNightRule;

        UnitReplace replace = new UnitReplace(mapGame.Units);

        skillCast = new HeroSkillCastController { dayNightRule = dayNightRule };

        playerSkillCast = new PlayerSkillCastController
        {
            dayNightRule = dayNightRule,
            buffManager = mapGame.BuffManager,
            mana = mapGame.PlayerManaManager,
            //heroSkillCast = skillCast,
        };
        if (playerSkillPanel != null) playerSkillPanel.Bind(playerSkillCast);
        view.skillCast = skillCast;
        view.playerSkillCast = playerSkillCast;

        RangeInfo rangeInfo = new RangeInfo();
        RangeTileData rangeStore = new RangeTileData();
        RangeCalc rangeCalc = new RangeCalc(rangeInfo);
        HoverPlaceData hoverPlace = new HoverPlaceData();

        if (tilePaintView != null)
        {
            PlaceHoverFinder hoverFinder = new PlaceHoverFinder(view, finder, hoverPlace);
            SkillTargetFinder skillFinder = new SkillTargetFinder(skillCast, view);
            PlayerSkillTargetFinder playerSkillFinder = new PlayerSkillTargetFinder(playerSkillCast, view);
            tilePaintView.SetupEdges(boards);
            tilePaintView.sync = new TilePaintSync(hoverFinder, skillFinder, playerSkillFinder, rangeCalc, rangeStore, tilePaintView.Painter, pointerPick);
        }

        rangeInput.pointerPick = pointerPick;
        rangeInput.rangeCalc = rangeCalc;
        rangeInput.rangeStore = rangeStore;

        view.pointerPick = pointerPick;
        view.replace = replace;
        view.citizenManager = mapGame.CitizenManager;
        view.resourcesManager = mapGame.ResourcesManager;

        PlaceAction action = new PlaceAction();
        action.palette = palette;
        action.placer = mapGame.Placer;
        action.remover = new UnitRemover(mapGame.Units, mapGame.HeroRoster);
        action.replace = replace;
        action.dayNightRule = dayNightRule;
        action.view = view;
        action.heroRoster = mapGame.HeroRoster;
        action.skillCast = skillCast;
        action.playerSkillCast = playerSkillCast;
        action.combineManager = combineManager;
        action.placeSoundKey = placeSoundKey;
        action.removeSoundKey = removeSoundKey;
        action.hoverPlace = hoverPlace;

        command.pointerPick = pointerPick;
        command.dragDetect = new DragDetect(dragPixels);
        command.rightDragDetect = new DragDetect(dragPixels);
        command.replace = replace;
        command.action = action;
        ghost = new PlaceGhost(ghostAlpha);
        command.ghost = ghost;
        command.hoverPlace = hoverPlace;
        command.gimmickPopup = gimmickPopup;
        command.dispatch = new Dictionary<PlaceMode, Action<Tile>>
        {
            { PlaceMode.Off, action.SelectTile },
            { PlaceMode.Place, action.PlaceUnit },
            { PlaceMode.Remove, action.RemoveUnit },
        };
        mapGame.Rule.ChangeToDay += trailState.StartDay;
        mapGame.Rule.ChangeToNight += trailState.StartNight;
        mapGame.EnviromentManager.OnDay += trailState.FinishDay;
        mapGame.EnviromentManager.OnNight += trailState.FinishNight;

        mapGame.Rule.ChangeToDay += OnDayChanged;
        OnDayChanged(); // 첫 날짜도 시작하자마자 바로 맞춘다 — 이벤트가 처음 울릴 때까지 기다리지 않는다

        desertBoard.Module.OnStateChanged += RefreshDesertDay;

        RegisterZoneEffect(desertZoneEffect);
        RegisterZoneEffect(campfireLights);
        for (int index = 0; index < iceZoneEffects.Count; index++)
        {
            RegisterZoneEffect(iceZoneEffects[index]);
        }

        mapGame.Rule.ChangeToDay += OnFireDayChanged;
        OnFireDayChanged(); // 첫 날도 낮이니 꺼진 채로 시작
        mapGame.Rule.ChangeToNight += OnFireNightChanged;
        FireReceiver.SetGameManager(mapGame.Rule);

        mapGame.Rule.ChangeToNight += view.ClearMode;
        mapGame.Rule.ChangeToNight += skillCast.ClearSelection;
        mapGame.Rule.ChangeToDay += playerSkillCast.ClearArmed;

        // 확장 이벤트: 5일마다 GameManager가 쏘고, 밤이 되면 선택을 무른다.
        // 미배선이면 확장만 꺼지고 나머지 조립은 그대로 돈다.
        if (expand != null)
        {
            mapGame.Rule.ChangeToNight += expand.CancelChoices;
        }

        started = true;
    }

    private void OnDestroy()
    {
        if (!started)
        {
            return;
        }

        ghost.ClearGhosts();
        mapGame.Rule.ChangeToNight -= view.ClearMode;
        desertBoard.Module.OnStateChanged -= RefreshDesertDay;
        UnregisterZoneEffect(desertZoneEffect);
        desertZoneEffect.Dispose();
        if (campfireLights != null)
        {
            UnregisterZoneEffect(campfireLights);
        }
        if (iceZoneEffects != null)
        {
            for (int index = 0; index < iceZoneEffects.Count; index++)
            {
                UnregisterZoneEffect(iceZoneEffects[index]);
            }
        }
        mapGame.Rule.ChangeToDay -= OnFireDayChanged;
        mapGame.Rule.ChangeToNight -= OnFireNightChanged;
        if (laneModules != null)
        {
            mapGame.Rule.ChangeToDay -= OnDayChanged;
        }
        if (skillCast != null)
        {
            mapGame.Rule.ChangeToNight -= skillCast.ClearSelection;
        }
        if (playerSkillCast != null)
        {
            mapGame.Rule.ChangeToDay -= playerSkillCast.ClearArmed;
        }
        if (expand != null)
        {
            mapGame.Rule.ChangeToNight -= expand.CancelChoices;
        }
        mapGame.Rule.ChangeToDay -= trailState.StartDay;
        mapGame.Rule.ChangeToNight -= trailState.StartNight;
        mapGame.EnviromentManager.OnDay -= trailState.FinishDay;
        mapGame.EnviromentManager.OnNight -= trailState.FinishNight;
        trailState.Release();
    }

    // 사막이 세이브 복원이나 확장으로 뒤늦게 열리면 놓친 낮 준비를 다시 맞춘다.
    private void RefreshDesertDay(ModuleState state)
    {
        ApplyDesertDay(mapGame.Rule.CanBuild);
    }

    // 낮일 때만 사막의 낮 준비를 다시 실행한다.
    private void ApplyDesertDay(bool isDay)
    {
        if (!isDay)
        {
            return;
        }

        desertZoneEffect.OnDayChanged();
    }

    // 지대 효과를 낮/밤 이벤트에 연결하고 첫 상태를 낮으로 맞춘다.
    private void RegisterZoneEffect(IZoneEffect effect)
    {
        mapGame.Rule.ChangeToDay += effect.OnDayChanged;
        mapGame.Rule.ChangeToNight += effect.OnNightChanged;
        effect.OnDayChanged();
    }

    // 등록해둔 지대 효과를 낮/밤 이벤트에서 뗀다.
    private void UnregisterZoneEffect(IZoneEffect effect)
    {
        mapGame.Rule.ChangeToDay -= effect.OnDayChanged;
        mapGame.Rule.ChangeToNight -= effect.OnNightChanged;
    }

    // 레지스트리에 등록된 모듈들의 보드·트레일·레인 목록을 한 번의 순회로 모은다. 모듈 루트에 ModuleLogic과 MapBoard가 함께 산다.
    private void CollectModuleComponents(
        out List<MapBoard> boards,
        out List<EnemyLanes> lanes)
    {
        boards = new List<MapBoard>();
        lanes = new List<EnemyLanes>();

        foreach (ModuleLogic logic in registry.AllModules.Values)
        {
            boards.Add(logic.GetComponent<MapBoard>());

            PathTrail trail = logic.GetComponent<PathTrail>();
            if (trail != null)
            {
                trailState.Collect(logic, trail);
            }

            EnemyLanes lane = logic.GetComponent<EnemyLanes>();
            if (lane != null)
            {
                lanes.Add(lane);
            }
        }
    }

    // 모든 얼음 보드의 고정 모닥불 보호 영역과 그 자리에 놓인 불빛, 지대 효과를 시작할 때 한 번 만듭니다.
    private void BuildCampfires(List<MapBoard> boards)
    {
        CampfireCalc calc = new();
        campfireLights = new CampfireLightController();
        iceZoneEffects = new List<IceZoneEffect>();
        iceSnowfalls = new List<IceSnowfall>();
        GameObject snowPrefab = Resources.Load<GameObject>("ZoneEffectPrefab/IceSnowfallVFX");
        for (int index = 0; index < boards.Count; index++)
        {
            MapBoard board = boards[index];
            IceZone iceZone = board.GetComponent<IceZone>();
            if (iceZone != null)
            {
                CampfireData data = calc.BuildData(board.Cells, iceZone.CampfireRange);
                iceZone.SetCampfire(data);
                campfireLights.Collect(board, iceZone.CampfireRange);
                iceZoneEffects.Add(new IceZoneEffect(board, mapGame.Units, iceZone));
                SnowMeltZone meltZone = new SnowMeltZone(board, iceZone.CampfireRange);
                iceSnowfalls.Add(new IceSnowfall(board, snowPrefab, meltZone.Zones));
            }
        }
    }

    // 날짜가 바뀔 때마다 모든 모듈의 레인을 그 날짜로 다시 계산한다.
    private void OnDayChanged()
    {
        int day = mapGame.Rule.DayCount;
        for (int i = 0; i < laneModules.Count; i++)
        {
            laneModules[i].RefreshForDay(day);
        }
    }

    // 낮이 되면 불 칸이 대미지를 끊도록 알린다.
    private void OnFireDayChanged()
    {
        FireReceiver.SetNight(false);
    }

    // 밤이 되면 불 칸이 대미지를 넣도록 알린다.
    private void OnFireNightChanged()
    {
        FireReceiver.SetNight(true);
        IgniteStandingUnits();
    }

    // 낮에 배치돼 진입 신호를 놓친 유닛도 밤이 오면 불 칸이면 점화한다.
    private void IgniteStandingUnits()
    {
        for (int i = 0; i < mapGame.Units.Count; i++)
        {
            GameObject unit = mapGame.Units.UnitAt(i);
            PlacementArea area = mapGame.Units.AreaAt(i);
            Tile tile = area.Board.Cells[area.Origin];
            FireReceiver.ReceiveEntry(tile, unit.transform);
        }
    }
}
