using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

public class ResourceTest : MonoBehaviour
{
    [SerializeField] private List<ProductionFacility> facilityPrefabs;
    [SerializeField] private List<House> housePrefabs;
    private List<ProductionFacility> obj = new();
    private List<House> houses = new();
    private ResourcesManager manager;
    private UiManager uiManager;
    private GameManager gameManager;
    private BuildingPool buildingPool;
    private Dictionary<ProductionType, ProductionFacility> _prefabs;
    [SerializeField] private MapBoard board;
    private IObjectResolver resolver;

    [Inject]
    private void Construct(IObjectResolver resolver, ResourcesManager resourcesManager, UiManager uiManager, GameManager gameManager, BuildingPool buildingPool, BuildingPrefabRegistry registry)
    {
        this.resolver = resolver;
        manager = resourcesManager;
        this.uiManager = uiManager;
        this.gameManager = gameManager;
        this.buildingPool = buildingPool;
        _prefabs = registry.Prefabs;
    }

    //private void Start()
    //{
    //    board.Occupied += buildingPool.TryRent;
    //}

    //private void OnDestroy()
    //{
    //    board.Occupied -= buildingPool.TryRent;
    //}

    private void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (manager.CheckResources(_prefabs[ProductionType.Wood].BasicValue.ConstructProduct) && gameManager.CanBuild)
                obj.Add(buildingPool.Rent(ProductionType.Wood));
            else
            {
                if(gameManager.CanBuild)
                    Debug.LogWarning("자원이 부족합니다.");
                else
                    Debug.LogWarning("밤에는 건설 할 수 없습니다.");
            }
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (manager.CheckResources(_prefabs[ProductionType.Food].BasicValue.ConstructProduct) && gameManager.CanBuild)
                obj.Add(buildingPool.Rent(ProductionType.Food));
            else
            {
                if (gameManager.CanBuild)
                    Debug.LogWarning("자원이 부족합니다.");
                else
                    Debug.LogWarning("밤에는 건설 할 수 없습니다.");
            }
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (manager.CheckResources(_prefabs[ProductionType.Gold].BasicValue.ConstructProduct) && gameManager.CanBuild)
                obj.Add(buildingPool.Rent(ProductionType.Gold));
            else
            {
                if (gameManager.CanBuild)
                    Debug.LogWarning("자원이 부족합니다.");
                else
                    Debug.LogWarning("밤에는 건설 할 수 없습니다.");
            }
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            if (manager.CheckResources(_prefabs[ProductionType.Iron].BasicValue.ConstructProduct) && gameManager.CanBuild)
                obj.Add(buildingPool.Rent(ProductionType.Iron));
            else
            {
                if (gameManager.CanBuild)
                    Debug.LogWarning("자원이 부족합니다.");
                else
                    Debug.LogWarning("밤에는 건설 할 수 없습니다.");
            }
        }

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            if (manager.CheckResources(_prefabs[ProductionType.Stone].BasicValue.ConstructProduct) && gameManager.CanBuild)
                obj.Add(buildingPool.Rent(ProductionType.Stone));
            else
            {
                if (gameManager.CanBuild)
                    Debug.LogWarning("자원이 부족합니다.");
                else
                    Debug.LogWarning("밤에는 건설 할 수 없습니다.");
            }
        }

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            var house = housePrefabs[Random.Range(0, housePrefabs.Count)];
            if (manager.CheckResources(house.Resources) && gameManager.CanBuild)
                houses.Add(resolver.Instantiate(house));
            else
            {
                if (gameManager.CanBuild)
                    Debug.LogWarning("자원이 부족합니다.");
                else
                    Debug.LogWarning("밤에는 건설 할 수 없습니다.");
            }
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

            if(Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            {
                var building = hit.collider.GetComponent<ProductionFacility>();
                if (building != null)
                {
                    uiManager.OpenBuildingUi(building);
                }
            }
            else
            {
                if (uiManager.BuildingUiOpen)
                {
                    uiManager.CloseBuildingUi();
                }
            }
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            foreach(var facility in obj)
            {
                facility.TakeDamage(100);
            }
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (!gameManager.CanBuild)
                gameManager.OnResult();
        }
    }
}
