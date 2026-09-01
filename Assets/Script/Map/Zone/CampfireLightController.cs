// 얼음 보드의 모닥불 불빛 상태를 지금에 맞게 적용한다.
public class CampfireLightController : IZoneEffect
{
    // +2칸: 감쇠 탓에 보호 반경을 딱 맞추면 오히려 좁아 보여 넉넉하게 넘치도록 잡는다.
    private const float ReachMarginCells = 2f;

    private readonly CampfireLightStore lightStore;

    // 보관된 불빛 묶음을 받아 보드마다 비추는 범위를 보호 반경에 맞춘다.
    public CampfireLightController(CampfireLightStore lightStore)
    {
        this.lightStore = lightStore;
        ApplyEveryReach();
    }

    // 밤이 시작되면 해금된 보드의 불빛만 켠다. 잠긴 보드의 불빛은 꺼진 채로 남는다.
    public void OnNightChanged()
    {
        for (int index = 0; index < lightStore.GroupCount; index++)
        {
            SetGroupOn(lightStore.ReadLights(index), lightStore.ReadBoard(index).IsUnlocked);
        }
    }

    // 낮이 시작되면 보관된 불빛을 전부 끈다.
    public void OnDayChanged()
    {
        for (int index = 0; index < lightStore.GroupCount; index++)
        {
            SetGroupOn(lightStore.ReadLights(index), false);
        }
    }

    // 보드마다 불빛이 비추는 범위를 그 보드의 보호 반경에 맞춘다.
    private void ApplyEveryReach()
    {
        for (int index = 0; index < lightStore.GroupCount; index++)
        {
            SetGroupReach(lightStore.ReadLights(index), ReadWorldReach(lightStore.ReadBoard(index), lightStore.ReadZone(index)));
        }
    }

    // 보호 반경에 여유 칸을 더해 월드 단위 비추는 범위를 낸다.
    private static float ReadWorldReach(MapBoard iceBoard, IceZone iceZone)
    {
        return (iceZone.CampfireRange + ReachMarginCells) * iceBoard.CellSize;
    }

    // 불빛 묶음이 비추는 범위를 한꺼번에 맞춘다.
    private static void SetGroupReach(CampfireLight[] lightGroup, float worldReach)
    {
        for (int index = 0; index < lightGroup.Length; index++)
        {
            lightGroup[index].SetRange(worldReach);
        }
    }

    // 불빛 묶음을 한꺼번에 같은 켜짐 상태로 맞춘다.
    private static void SetGroupOn(CampfireLight[] lightGroup, bool turnOn)
    {
        for (int index = 0; index < lightGroup.Length; index++)
        {
            lightGroup[index].SetOn(turnOn);
        }
    }
}
