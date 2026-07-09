using UnityEngine;
using VContainer;

public class UiManager : MonoBehaviour
{
    [SerializeField] private BuildingPanel buildingUi;
    public bool BuildingUiOpen => buildingUi.gameObject.activeSelf;

    private void Awake()
    {
        buildingUi.gameObject.SetActive(false);
    }

    public void OpenBuildingUi(ProductionFacility facility)
    {
        buildingUi.gameObject.SetActive(true);
        buildingUi.InitFacilityInfo(facility);
    }

    public void CloseBuildingUi()
    {
        buildingUi.gameObject.SetActive(false);
    }
}
