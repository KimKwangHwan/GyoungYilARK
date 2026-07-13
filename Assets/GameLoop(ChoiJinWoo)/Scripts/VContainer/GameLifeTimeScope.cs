using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifeTimeScope : LifetimeScope
{
    [SerializeField] private ResourcesManager resourcesManagerPrefab;
    [SerializeField] private CitizenManager citizenManagerPrefab;
    [SerializeField] private UiManager UiManagerPrefab;
    [SerializeField] private EnviromentManager EnviromentManagerPrefab;
    [SerializeField] private GameManager GameManagerPrefab;
    [SerializeField] private FacilityManager FacilityManagerPrefab;
    [SerializeField] private BuildingPrefabRegistry buildingPrefabRegistry;
    [SerializeField] private Canvas UiManagerParent;
    [SerializeField] private Light sunLight;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInNewPrefab(resourcesManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterComponentInNewPrefab(citizenManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterComponentInNewPrefab(UiManagerPrefab, Lifetime.Singleton).UnderTransform(UiManagerParent.transform).AsSelf();
        builder.RegisterComponentInNewPrefab(EnviromentManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterComponentInNewPrefab(GameManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterComponentInNewPrefab(FacilityManagerPrefab, Lifetime.Singleton).AsSelf();
        builder.RegisterInstance(buildingPrefabRegistry);
        builder.Register<BuildingPool>(Lifetime.Singleton);

        // 생성된 인스턴스에 씬의 sunLight를 넘겨주는 콜백
        builder.RegisterBuildCallback(resolver =>
        {
            var toggle = resolver.Resolve<EnviromentManager>();
            toggle.SetSunLight(sunLight);
        });

        builder.RegisterComponentInHierarchy<ResourceTest>();
        builder.RegisterComponentInHierarchy<TopBar>();
        builder.RegisterComponentInHierarchy<DayNightButton>();
        builder.RegisterComponentInHierarchy<ExpeditionButton>();
    }
}
