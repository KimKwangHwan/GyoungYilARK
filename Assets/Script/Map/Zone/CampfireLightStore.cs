using System.Collections.Generic;

// 얼음 보드마다 찾아낸 모닥불 불빛 묶음을 들고 있는다.
public class CampfireLightStore
{
    private readonly List<MapBoard> iceBoards = new();
    private readonly List<IceZone> iceZones = new();
    private readonly List<CampfireLight[]> lightGroups = new();

    public int GroupCount => iceBoards.Count;

    // 한 얼음 보드에서 찾아낸 불빛 묶음을 그 보드·구역과 나란히 보관한다.
    public void KeepGroup(MapBoard iceBoard, IceZone iceZone, CampfireLight[] lightGroup)
    {
        iceBoards.Add(iceBoard);
        iceZones.Add(iceZone);
        lightGroups.Add(lightGroup);
    }

    // 순번에 해당하는 얼음 보드를 돌려준다.
    public MapBoard ReadBoard(int index)
    {
        return iceBoards[index];
    }

    // 순번에 해당하는 얼음 구역 부품을 돌려준다.
    public IceZone ReadZone(int index)
    {
        return iceZones[index];
    }

    // 순번에 해당하는 불빛 묶음을 돌려준다.
    public CampfireLight[] ReadLights(int index)
    {
        return lightGroups[index];
    }
}
