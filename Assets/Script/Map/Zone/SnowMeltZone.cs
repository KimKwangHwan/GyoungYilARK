using System.Collections.Generic;
using UnityEngine;

// 모닥불 자리마다 눈송이가 사라질 구역을 만들어 보관한다.
public class SnowMeltZone
{
    private const int IgnoreRaycastLayer = 2;
    private const string ZoneObjectName = "SnowMeltZone";

    private readonly List<Collider> zones = new();

    public IReadOnlyList<Collider> Zones => zones;

    // 넘겨받은 모닥불 불빛 자리마다 보호 반경만 한 구역을 만들어 담는다.
    public SnowMeltZone(MapBoard iceBoard, int campfireRange, CampfireLight[] lightGroup)
    {
        float meltRadius = campfireRange * iceBoard.CellSize;

        for (int index = 0; index < lightGroup.Length; index++)
        {
            zones.Add(BuildZone(lightGroup[index].transform.position, meltRadius, iceBoard.transform));
        }
    }

    // 지정 위치에 눈송이만 반응하는 구 모양 구역 하나를 만든다.
    private static Collider BuildZone(Vector3 center, float radius, Transform parent)
    {
        GameObject zoneObject = new GameObject(ZoneObjectName);
        zoneObject.layer = IgnoreRaycastLayer;
        zoneObject.transform.SetParent(parent, false);
        zoneObject.transform.position = center;

        SphereCollider sphere = zoneObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = radius;
        return sphere;
    }
}
