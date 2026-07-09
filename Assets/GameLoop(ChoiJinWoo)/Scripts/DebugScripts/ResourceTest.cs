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
    private IObjectResolver resolver;

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        this.resolver = resolver;
    }

    [Inject]
    public void Construct(ResourcesManager resourcesManager)
    {
        manager = resourcesManager;
    }

    [Inject]
    public void Construct(UiManager uiManager)
    {
        this.uiManager = uiManager;
    }

    private void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            Debug.Log("pressed");
            obj.Add(resolver.Instantiate(facilityPrefabs[0]));
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            Debug.Log("pressed");
            obj.Add(resolver.Instantiate(facilityPrefabs[1]));
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            Debug.Log("pressed");
            obj.Add(resolver.Instantiate(facilityPrefabs[2]));
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            Debug.Log("pressed");
            obj.Add(resolver.Instantiate(facilityPrefabs[3]));
        }

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            Debug.Log("pressed");
            obj.Add(resolver.Instantiate(facilityPrefabs[4]));
        }

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            Debug.Log("pressed");
            houses.Add(resolver.Instantiate(housePrefabs[Random.Range(0, housePrefabs.Count)]));
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

        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            Debug.Log("pressed");
            if (obj != null)
            {
                foreach(var item in obj)
                {
                    item.ProduceProduction();
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
    }
}
