using Cysharp.Threading.Tasks;
using VContainer.Unity;

// 씬이 켜질 때 저장된 슬롯을 읽어 게임 상태에 되돌린다.
public class LoadManager : IStartable
{
    private readonly SaveSlot saveSlot;
    private readonly SaveRestore saveRestore;
    private readonly DayNightButton dayNightButton;
    private readonly SaveTimeData saveTimeData;
    private readonly GameManager gameManager;
    private readonly SaveManager saveManager;
    private readonly TrailStateController trailState;

    // 로드에 필요한 저장 슬롯·복원기·버튼·시간 데이터·게임 진행·저장 잠금을 받아 둔다
    public LoadManager(
        SaveSlot saveSlot,
        SaveRestore saveRestore,
        DayNightButton dayNightButton,
        SaveTimeData saveTimeData,
        GameManager gameManager,
        SaveManager saveManager,
        TrailStateController trailState)
    {
        this.saveSlot = saveSlot;
        this.saveRestore = saveRestore;
        this.dayNightButton = dayNightButton;
        this.saveTimeData = saveTimeData;
        this.gameManager = gameManager;
        this.saveManager = saveManager;
        this.trailState = trailState;
    }

    // 모든 Start()가 끝난 다음 프레임에 한 번만 로드한다.
    public void Start()
    {
        if (SelectedSaveSlot.IsNewGame)
        {
            trailState.FinishDayLoad();
            return;
        }

        WaitedLoad().Forget();
    }

    // Start()가 끝난 다음 프레임에 한 번만 로드한다
    private async UniTaskVoid WaitedLoad()
    {
        await UniTask.Yield();
        TryLoad();
    }

    // 선택 상태에 맞는 저장본을 읽어 적용한다.
    private bool TryLoad()
    {
        if (!TryReadSelectedSave(out SaveFile saveFile)) return false;

        ApplyLoaded(saveFile.saveData);
        return true;
    }

    // 선택된 일차가 있으면 그 일차 파일을, 없으면 최신 저장본을 읽는다.
    private bool TryReadSelectedSave(out SaveFile saveFile)
    {
        if (SelectedSaveSlot.SelectedDay == SelectedSaveSlot.NoSelectedDay)
        {
            return saveSlot.TryReadLatest(SelectedSaveSlot.SlotId, out saveFile);
        }

        return saveSlot.TryReadDay(SelectedSaveSlot.SlotId, SelectedSaveSlot.SelectedDay, out saveFile);
    }

    // 복원 전체를 저장 잠금으로 감싼다
    private void ApplyLoaded(SaveData data)
    {
        saveManager.LockSaveForLoad();
        try
        {
            RestoreAll(data);
        }
        finally
        {
            saveManager.UnlockSaveForLoad();
        }
    }

    // 검증된 데이터를 적용하고 저장 단계에 맞는 후처리를 실행한다
    private void RestoreAll(SaveData data)
    {
        saveRestore.RestoreSaveData(data);
        saveTimeData.SetPlayTime(data.playTime);
        saveTimeData.SetDayList(data.savedDayList);
        dayNightButton.RefreshDayText();
        saveRestore.RestoreRegionNotice(data.regionUnlockNoticeSeen);

        ApplyDayStartPhase(data);
        ApplyDayTrailPhase(data);
        ApplyNightReadyPhase(data);
    }

    // DayStart 저장본에만 생산과 완벽방어 보상을 한 번 적용한다
    private void ApplyDayStartPhase(SaveData data)
    {
        if (data.savePhase != SavePhase.DayStart) return;

        saveRestore.ApplyProduction();
        if (data.perfectDefensePending)
        {
            saveRestore.ApplyPerfectDefenseReward();
        }
    }

    // 완성된 낮 저장본은 복원 완료 뒤 Trail 반복 재생을 시작한다
    private void ApplyDayTrailPhase(SaveData data)
    {
        bool isDayPhase = IsDayPhase(data.savePhase);
        if (!isDayPhase) return;

        trailState.FinishDayLoad();
    }

    // 낮 Trail을 보여야 하는 저장 단계를 계산한다
    private static bool IsDayPhase(SavePhase savePhase)
    {
        return savePhase == SavePhase.DayStart ||
               savePhase == SavePhase.DayActive;
    }

    // NightReady 저장본에만 밤 입력을 잠그고 밤 전환을 시작한다
    private void ApplyNightReadyPhase(SaveData data)
    {
        if (data.savePhase != SavePhase.NightReady) return;

        dayNightButton.RestoreNightLock();
        gameManager.OnNight();
    }
}
