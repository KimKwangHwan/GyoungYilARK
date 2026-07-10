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
    private IObjectResolver resolver;

    [Inject]
    private void Construct(IObjectResolver resolver, ResourcesManager resourcesManager, UiManager uiManager, GameManager gameManager)
    {
        this.resolver = resolver;
        manager = resourcesManager;
        this.uiManager = uiManager;
        this.gameManager = gameManager;
    }

    private void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (manager.CheckResources(facilityPrefabs[0].BasicValue.ConstructProduct) && gameManager.CanBuild)
                obj.Add(resolver.Instantiate(facilityPrefabs[0]));
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
            if (manager.CheckResources(facilityPrefabs[1].BasicValue.ConstructProduct) && gameManager.CanBuild)
                obj.Add(resolver.Instantiate(facilityPrefabs[1]));
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
            if (manager.CheckResources(facilityPrefabs[2].BasicValue.ConstructProduct) && gameManager.CanBuild)
                obj.Add(resolver.Instantiate(facilityPrefabs[2]));
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
            if (manager.CheckResources(facilityPrefabs[3].BasicValue.ConstructProduct) && gameManager.CanBuild)
                obj.Add(resolver.Instantiate(facilityPrefabs[3]));
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
            if (manager.CheckResources(facilityPrefabs[4].BasicValue.ConstructProduct) && gameManager.CanBuild)
                obj.Add(resolver.Instantiate(facilityPrefabs[4]));
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
