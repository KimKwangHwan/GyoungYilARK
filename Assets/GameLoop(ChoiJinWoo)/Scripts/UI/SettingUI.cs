using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SettingUI : MonoBehaviour, IExclusiveUiPanel
{
    [SerializeField] private TMP_Dropdown screenMode;
    [SerializeField] private AudioMixer gameAudioMixer;
    [SerializeField] private Slider masterVolume;
    [SerializeField] private Slider bgmVolume;
    [SerializeField] private Slider sfxVolume;
    [SerializeField] private Slider systemVolume;
    // 전용 전체화면(ExclusiveFullScreen)은 D3D12 백엔드에서 Print Screen/Alt+Tab 등
    // 전체화면 상태 전환 시 복구 불가능한 GPU 디바이스 오류로 크래시하므로 제외한다.
    // "전체 화면" 항목은 테두리 없는 창(FullScreenWindow)에 매핑한다.
    private readonly FullScreenMode[] screenModes =
    {
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed,
    };
    private Resolution[] resolutions;
    [SerializeField] private TMP_Dropdown screenWide;
    private static readonly (int width, int height)[] commonResolutions =
    {
        (1280, 720),
        (1600, 900),
        (1920, 1080),
        (2560, 1440),
        (3840, 2160),
    };

    private (Slider slider, string param)[] VolumeSliders => new[]
    {
        (masterVolume, "MasterVolume"),
        (bgmVolume, "BgmVolume"),
        (sfxVolume, "SfxVolume"),
        (systemVolume, "System"),
    };

    [SerializeField] private RectTransform openButton; // 설정 패널을 여는 버튼 - alsoSelf로 넘겨야 열려있을 때 눌러도 꺼졌다 켜지는 깜빡임이 안 생긴다
    private ClickOutsideCloser outsideCloser;
    // 타이틀 화면처럼 단독으로 쓰일 때는 이 패널 자신에 PanelReveal이 붙어 있을 수 있다 - MenuUI에
    // 담겨 있을 때는 부모 쪽 PanelReveal이 대신 책임지므로 이건 null인 채로 둔다.
    private PanelReveal panelReveal;

    private void Awake()
    {
        outsideCloser = new ClickOutsideCloser((RectTransform)transform, openButton);
        panelReveal = GetComponent<PanelReveal>();
    }

    private void OnEnable()
    {
        outsideCloser.MarkOpened();
        // MenuUI에 담겨 있을 때(Closed 구독자가 있을 때)는 등록하지 않는다 - 이미 부모(MenuUI)가
        // ExclusiveUiCoordinator에 등록돼 있는데 자식인 자신까지 등록하면, 메뉴가 열리자마자
        // "다른 패널이 열렸다"는 신호로 자기 자신(=부모)을 즉시 닫아버린다. 타이틀 화면처럼
        // 단독으로 쓰일 때만(구독자 없음) 참여한다.
        if (Closed == null) ExclusiveUiCoordinator.NotifyOpened(this);
        GlobalUiInputSignals.ClickPerformed += HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed += HandleCloseCheck;

        //창 모드
        screenMode.ClearOptions();

        // 이 목록은 아래 RefreshDropdown()에도 같은 순서로 존재한다 - screenModes 배열과
        // 항목 수가 어긋나면 ChangeScreenMode에서 IndexOutOfRange가 나므로 함께 수정할 것.
        var options = new List<string>
        {
            DataTableManager.StringTable.Get("UI_Setting_FullScreen"), // FullScreenWindow에 매핑
            DataTableManager.StringTable.Get("UI_Setting_Window"),
        };
        screenMode.AddOptions(options);

        int savedMode = PlayerPrefs.GetInt("ScreenMode", (int)FullScreenMode.FullScreenWindow);
        int modeIndex = Array.IndexOf(screenModes, (FullScreenMode)savedMode);
        screenMode.value = modeIndex >= 0 ? modeIndex : 0;
        screenMode.RefreshShownValue();

        screenMode.onValueChanged.AddListener(ChangeScreenMode);

        LocalizeTextManager.OnLanguageChanged += RefreshDropdown;

        //오디오
        foreach (var (slider, param) in VolumeSliders)
        {
            slider.value = PlayerPrefs.GetFloat(param, 1f);
            ApplyVolume(param, slider.value);
            slider.onValueChanged.AddListener(v => SetVolume(param, v));
        }

        //해상도
        resolutions = Screen.resolutions
            .GroupBy(r => (r.width, r.height))
            .Select(g => g.First())
            .Where(r => commonResolutions.Contains((r.width, r.height)))
            .OrderBy(r => r.width)
            .ToArray();

        if (resolutions.Length == 0)
            resolutions = new[] { Screen.currentResolution };

        screenWide.ClearOptions();

        var screenoptions = new List<string>();
        int currentIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            screenoptions.Add($"{resolutions[i].width} x {resolutions[i].height}");
            if (resolutions[i].width == Screen.width &&
                resolutions[i].height == Screen.height)
            {
                currentIndex = i;
            }
        }

        screenWide.AddOptions(screenoptions);
        screenWide.value = currentIndex;
        screenWide.RefreshShownValue();

        screenWide.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void OnDisable()
    {
        if (Closed == null) ExclusiveUiCoordinator.NotifyClosed(this);
        GlobalUiInputSignals.ClickPerformed -= HandleCloseCheck;
        GlobalUiInputSignals.EscapePerformed -= HandleCloseCheck;

        foreach (var (slider, _) in VolumeSliders)
        {
            slider.onValueChanged.RemoveAllListeners();
        }
        screenMode.onValueChanged.RemoveAllListeners();
        screenWide.onValueChanged.RemoveAllListeners();
    }

    private void OnResolutionChanged(int index)
    {
        var res = resolutions[index];

        if (Screen.width == res.width && Screen.height == res.height)
            return;

        Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
        PlayerPrefs.SetInt("ResWidth", res.width);
        PlayerPrefs.SetInt("ResHeight", res.height);
    }

    // MenuUI처럼 이 패널을 자기 내용물로 품고 있는 상위 패널이 있으면, 이 패널만 닫히고 상위 패널은
    // 열린 채로 남아 텅 빈 화면이 되는 걸 막을 수 있게 닫힘을 알린다(구독하지 않으면 원래처럼 이 패널만 닫힘).
    public event System.Action Closed;

    // ExclusiveUiCoordinator가 다른 배타 패널이 열렸을 때 이 패널을 닫으라고 부르는 창구.
    public void RequestClose() => OnClose();

    public void OnClose()
    {
        // 구독자(MenuUI)가 있으면 그쪽이 PanelReveal로 축소 연출까지 책임지고 닫는다 - 여기서 먼저
        // 꺼버리면 상위 패널이 줄어들기도 전에 내용물만 뚝 사라져 버린다. 구독자가 없는 단독
        // 사용(타이틀 화면 등)일 때만 스스로 닫는데, 이때는 자기 PanelReveal이 있으면 그 축소
        // 연출로, 없으면 예전처럼 SetActive로 닫는다.
        if (Closed == null)
        {
            if (panelReveal != null) panelReveal.Hide();
            else gameObject.SetActive(false);
        }
        Closed?.Invoke();
    }

    private void HandleCloseCheck()
    {
        // MenuUI처럼 이 패널을 내용물로 품고 있는 상위 패널이 있으면(Closed 구독자가 있으면),
        // 바깥 클릭/ESC 판단은 그쪽에 전담시킨다 - 이 패널의 openButton은 단독 사용 시에만
        // 인스펙터에 꽂아주는 값이라 프리팹에서 비어 있는 경우가 많다. 그 상태로 여기서 또
        // 판정하면 메뉴를 여는 아이콘 클릭조차 "바깥 클릭"으로 오판해, 상위 패널의 정상적인
        // 토글과 따로 또 닫힘을 시도하며 서로 경합한다(연타 시 열림/닫힘이 꼬이는 원인).
        if (Closed != null) return;
        if (outsideCloser.ShouldClose())
        {
            OnClose();
        }
    }

    private void ChangeScreenMode(int index)
    {
        var mode = screenModes[index];
        if (Screen.fullScreenMode == mode) return;

        Screen.fullScreenMode = mode;
        PlayerPrefs.SetInt("ScreenMode", (int)mode); // 인덱스 대신 실제 enum 값 저장
    }

    private void SetVolume(string paramName, float sliderValue)
    {
        ApplyVolume(paramName, sliderValue);
        PlayerPrefs.SetFloat(paramName, sliderValue);
    }

    private void ApplyVolume(string paramName, float sliderValue)
    {
        gameAudioMixer.SetFloat(paramName, AudioVolumeUtil.LinearToDb(sliderValue));
    }

    private void RefreshDropdown()
    {
        screenMode.ClearOptions();

        // OnEnable의 목록과 항상 동일하게 유지할 것 (screenModes 배열과 항목 수 일치).
        var options = new List<string>
        {
            DataTableManager.StringTable.Get("UI_Setting_FullScreen"),
            DataTableManager.StringTable.Get("UI_Setting_Window"),
        };
        screenMode.AddOptions(options);
    }
}
