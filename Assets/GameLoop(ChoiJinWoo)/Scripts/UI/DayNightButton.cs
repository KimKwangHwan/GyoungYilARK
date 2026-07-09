using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class DayNightButton : MonoBehaviour
{
    [SerializeField] private Button button;
    private EnviromentManager enviromentManager;

    [Inject]
    private void Construct(EnviromentManager enviromentManager)
    {
        this.enviromentManager = enviromentManager;
    }

    private void Start()
    {
        button.onClick.AddListener(OnButton);
    }

    private void OnButton()
    {
        enviromentManager.ToggleDayNight();
    }

    private void OnDestroy()
    {
        button.onClick.RemoveAllListeners();
    }
}
