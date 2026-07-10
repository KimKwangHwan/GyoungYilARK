using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class ExpeditionButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private EnviromentManager enviromentManager;
    private UiManager uiManager;

    [Inject]
    private void Construct(EnviromentManager enviromentManager, UiManager uiManager)
    {
        this.enviromentManager = enviromentManager;
        this.uiManager = uiManager;
    }

    private void Awake()
    {
        button.onClick.AddListener(uiManager.CloseExpeditoinUi);
        button.onClick.AddListener(enviromentManager.ToggleDayNight);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveAllListeners();
    }
}
