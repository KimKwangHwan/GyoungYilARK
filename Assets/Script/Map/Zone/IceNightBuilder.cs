using System.Collections.Generic;
using UnityEngine;

// 얼음 구역이 밤에 쓸 담당들을 만들어 낸다.
public class IceNightBuilder
{
    private const string SnowPrefabPath = "ZoneEffectPrefab/IceSnowfallVFX";
    private const int FrontIndex = 0;

    private readonly CampfireLightFinder lightFinder = new();
    private readonly CampfireLightStore lightStore = new();
    private readonly CampfireCalc campfireCalc = new();
    private readonly List<IZoneEffect> zoneEffects = new();
    private readonly List<IceSnowfall> snowFalls = new();

    private readonly PlacedUnitData unitList;
    private readonly GameObject snowPrefab;

    public IReadOnlyList<IZoneEffect> Effects => zoneEffects;

    // 보드 목록에서 얼음 구역마다 밤 담당을 만들고, 불빛 점등 담당을 맨 앞에 세운다.
    public IceNightBuilder(IReadOnlyList<MapBoard> mapBoards, PlacedUnitData unitList)
    {
        this.unitList = unitList;
        snowPrefab = Resources.Load<GameObject>(SnowPrefabPath);

        BuildEveryBoard(mapBoards);
        zoneEffects.Insert(FrontIndex, new CampfireLightController(lightStore));
    }

    // 보드를 하나씩 넘기며 얼음 구역 담당 만들기를 맡긴다.
    private void BuildEveryBoard(IReadOnlyList<MapBoard> mapBoards)
    {
        for (int index = 0; index < mapBoards.Count; index++)
        {
            BuildBoardZones(mapBoards[index]);
        }
    }

    // 이 보드에 붙은 얼음 구역 부품 수만큼 담당을 만든다. 안 붙은 보드는 한 번도 만들지 않는다.
    private void BuildBoardZones(MapBoard mapBoard)
    {
        IceZone[] iceZones = mapBoard.GetComponents<IceZone>();
        for (int index = 0; index < iceZones.Length; index++)
        {
            BuildOneZone(mapBoard, iceZones[index]);
        }
    }

    // 얼음 구역 하나의 보호 데이터·불빛 보관·디버프 담당·눈발을 차례로 만든다.
    private void BuildOneZone(MapBoard iceBoard, IceZone iceZone)
    {
        iceZone.SetCampfire(campfireCalc.BuildData(iceBoard.Cells, iceZone.CampfireRange));

        CampfireLight[] boardLights = lightFinder.FindLights(iceBoard);
        lightStore.KeepGroup(iceBoard, iceZone, boardLights);

        zoneEffects.Add(new IceZoneEffect(iceBoard, unitList, iceZone));
        SnowMeltZone meltZone = new SnowMeltZone(iceBoard, iceZone.CampfireRange, boardLights);
        snowFalls.Add(new IceSnowfall(iceBoard, snowPrefab, meltZone.Zones));
    }
}
