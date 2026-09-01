using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifeTimeScope : LifetimeScope
{
    [SerializeField] private ResourcesManager resourcesManagerPrefab;
    [SerializeField] private CitizenManager citizenManagerPrefab;
    [SerializeField] private CitizenWanderManager citizenWanderManagerPrefab;
    [SerializeField] private UiManager UiManagerPrefab;
    [SerializeField] private EnviromentManager EnviromentManagerPrefab;
    [SerializeField] private GameManager GameManagerPrefab;
    [SerializeField] private PlayerManaManager playerManaManagerPrefab;
    [SerializeField] private FacilityManager FacilityManager;
    [SerializeField] private ProductionEconomyConfig economyConfig;
    [SerializeField] private ResourceIconSet resourceIconSet;
    [SerializeField] private HeroUpgradeConfig heroUpgradeConfig;
    [SerializeField] private HeroClassUpgradeConfig heroClassUpgradeConfig;
    [SerializeField] private List<BaseUpgradeData> heroStatUpgrades;
    [SerializeField] private Light sunLight;
    [SerializeField] private Transform citizenHubPoint;
    [SerializeField] private Transform[] citizenHomePoints; // 밤에 귀가할 목적지 후보들(여러 개면 시민마다 랜덤 선택)

    protected override void Configure(IContainerBuilder builder)
    {

        builder.RegisterComponentInHierarchy<ExpandEvent>().AsSelf();

        builder.RegisterBuildCallback(resolver =>
        {
            var ui = resolver.Resolve<UiManager>();
            resolver.InjectGameObject(ui.gameObject);
        });
        
        builder.RegisterComponentInNewPrefab(resourcesManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterComponentInNewPrefab(citizenManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterComponentInNewPrefab(citizenWanderManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterComponentInNewPrefab(UiManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterComponentInNewPrefab(EnviromentManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterComponentInNewPrefab(GameManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterComponentInNewPrefab(playerManaManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterInstance(economyConfig);
        builder.RegisterInstance(resourceIconSet);
        builder.RegisterInstance(heroUpgradeConfig);
        builder.RegisterInstance(heroClassUpgradeConfig);
        builder.RegisterInstance(heroStatUpgrades);
        builder.RegisterComponentOnNewGameObject<PoolManager>(Lifetime.Singleton).AsSelf();
        builder.Register<FacilityManager>(Lifetime.Singleton).AsSelf();
        builder.Register<BaseConstructor>(Lifetime.Singleton).AsSelf();
        builder.Register<BuffManager>(Lifetime.Singleton).As<ITickable>().AsSelf();
        builder.Register<HeroRoster>(Lifetime.Singleton).AsSelf();
        builder.Register<UpgradeState>(Lifetime.Singleton);
        builder.Register<HeroTierUpgradeState>(Lifetime.Singleton);
        builder.Register<HeroClassUpgradeState>(Lifetime.Singleton);
        builder.Register<HeroStatManager>(Lifetime.Singleton).AsSelf();
        // 아무도 생성자 의존성으로 요구하지 않아 lazy 등록만으로는 안 만들어진다 - 강제로 Resolve해서
        // 생성자가 돌게(=LevelChanged 구독이 걸리게) 한다. 안 하면 Hero.Awake가 정적 캐시를 처음
        // 읽는 순간 HeroStatManager 인스턴스가 없어 NullReferenceException이 난다.
        builder.RegisterBuildCallback(resolver =>
        {
            resolver.Resolve<HeroStatManager>();
        });
        builder.Register<UiPanelStack>(Lifetime.Singleton).AsSelf();
        builder.Register<TutorialState>(Lifetime.Singleton).AsSelf();
        // 씬 오브젝트가 아니라 컨테이너가 소유한다 - 연출용 오브젝트가 없는 씬에서도 세이브 쪽이 안전하게 받는다.
        builder.Register<GimmickTileData>(Lifetime.Singleton).AsSelf();

        // 세이브·로드 담당들. File/은 서로 의존하고, Logic/은 씬의 각 매니저를 읽고 쓴다.
        builder.Register<SaveCheck>(Lifetime.Singleton).AsSelf();
        builder.Register<SaveIO>(Lifetime.Singleton).AsSelf();
        builder.Register<SaveSlot>(Lifetime.Singleton).AsSelf();
        builder.Register<SaveCapture>(Lifetime.Singleton).AsSelf();
        builder.Register<SaveRestore>(Lifetime.Singleton).AsSelf();
        builder.Register<SaveTimeData>(Lifetime.Singleton).As<ITickable>().AsSelf();
        builder.Register<SaveManager>(Lifetime.Singleton).As<IStartable>().AsSelf();
        builder.Register<TrailStateController>(Lifetime.Singleton).AsSelf();
        builder.Register<LoadManager>(Lifetime.Singleton).As<IStartable>().AsSelf();
        builder.Register<SaveExitHook>(Lifetime.Singleton).As<IStartable>().AsSelf();
        builder.Register<SaveChangeTracker>(Lifetime.Singleton).As<IStartable>().As<ITickable>().AsSelf();
        builder.Register<SaveKey>(Lifetime.Singleton).AsSelf();
        builder.Register<SaveCipher>(Lifetime.Singleton).AsSelf();
        builder.Register<DayNightData>(Lifetime.Singleton).AsSelf();

        if (sunLight != null)
        {
            builder.RegisterBuildCallback(resolver =>
            {
                var toggle = resolver.Resolve<EnviromentManager>();
                toggle.SetSunLight(sunLight);
            });
        }

        // RegisterComponentInNewPrefab도 다른 컴포넌트가 생성자 의존성으로 요구하지 않으면 생성되지 않으므로
        // 여기서 강제로 Resolve해 항상 만들어지게 하고, 씬의 Hub Transform을 곧바로 주입한다.
        builder.RegisterBuildCallback(resolver =>
        {
            var wanderManager = resolver.Resolve<CitizenWanderManager>();
            if (citizenHubPoint != null)
                wanderManager.SetHubPoint(citizenHubPoint);
            wanderManager.SetHomePoints(citizenHomePoints);
        });

        builder.RegisterComponentInHierarchy<TopBar>();
        builder.RegisterComponentInHierarchy<DayNightButton>();
        builder.RegisterComponentInHierarchy<MenuUI>();
        builder.RegisterComponentInHierarchy<PlayerSkillPanel>();
        builder.RegisterComponentInHierarchy<MapGame>();
        builder.RegisterComponentInHierarchy<MapRegistry>();
        builder.RegisterComponentInHierarchy<MapAssemble>();
        builder.RegisterComponentInHierarchy<HeroRegistry>();
        builder.RegisterComponentInHierarchy<AddCitizen>();
        builder.RegisterComponentInHierarchy<SpawnerManager>().AsSelf();
        builder.RegisterComponentInHierarchy<FacilityBuildChoicePanel>();
        builder.RegisterComponentInHierarchy<CenterHubPanel>();
        builder.RegisterComponentInHierarchy<RegionOverviewPanel>();
        builder.RegisterComponentInHierarchy<RegionDetailPanel>();
        builder.RegisterComponentInHierarchy<BuildingPanel>();
        builder.RegisterComponentInHierarchy<HeroTierUpgradeMenu>();
        builder.RegisterComponentInHierarchy<HeroClassUpgradeMenu>();
        builder.RegisterComponentInHierarchy<HeroSetPanel>();
        builder.RegisterComponentInHierarchy<BuildModePanel>();
        builder.RegisterComponentInHierarchy<TutorialOverlayUI>();
        builder.RegisterComponentInHierarchy<TutorialManager>();
        builder.RegisterComponentInHierarchy<PlacePalette>(); // 튜토리얼이 배치 대기 상태(Mode)를 읽어 맵 클릭 순간 딤을 풀어주는 데 씀
        builder.RegisterComponentInHierarchy<MapView>(); // 튜토리얼이 RelocateHero 단계에서 재배치 완료(Replaced)를 구독하는 데 씀

        // PoolManager는 RegisterComponentOnNewGameObject라 아무도 Resolve하지 않으면 실제로 생성되지 않는다(lazy).
        // 여기서 강제로 한 번 Resolve해 _resolver가 붙은 상태로 즉시 만들어지게 한다.
        // (안 하면 스폰 경로가 전부 PoolManager.Instance의 미주입 폴백 인스턴스를 타게 됨)
        builder.RegisterBuildCallback(resolver =>
        {
            resolver.Resolve<PoolManager>();
        });

        // EnemyBase.Construct(PoolManager, WaveSpawner, GameManager)가 WaveSpawner 타입을 요구하므로
        // 컨테이너에 타입 등록 자체가 있어야 한다(InjectGameObject는 등록을 만들어주지 않음).
        // 여기서 잡히는 인스턴스는 스폰 직후 WaveSpawner.SpawnWaveRout의 enemy.SetOwner(this)로
        // 곧바로 실제 소유 스포너로 덮어써지므로, 어떤 WaveSpawner가 잡히든 무방하다.
        builder.RegisterComponentInHierarchy<WaveSpawner>();

        // WaveSpawner는 SpawnerManager.spawners에 인스펙터로만 연결돼 있어 컨테이너가 자동으로 찾지 못한다.
        // 씬의 모든 WaveSpawner를 찾아 직접 주입 → 각자의 Construct(PoolManager)가 실제로 호출되게 한다.
        builder.RegisterBuildCallback(resolver =>
        {
            var waveSpawners = FindObjectsByType<WaveSpawner>(FindObjectsSortMode.None);
            foreach (var ws in waveSpawners)
            {
                resolver.InjectGameObject(ws.gameObject);
            }
        });
    }
}
