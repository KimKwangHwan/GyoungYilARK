 using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
 /// 월드 위치에서 타일의 논리 좌표(Col/Row)를 계산해 새겨 넣는 에디터 전용 도구.
 /// origin을 원점(0,0) 기준으로 하여 각 타일 col/row 좌표수치 부여. 
 /// </summary>
public static class TilePosBaker
{
    // 1칸 = 1월드유닛. 큐브 프리팹이 1유닛 정사각형이라 고정. 런타임 MapBoard와 동일 가정.
    private const float CellSize = 1f;

    [MenuItem("Tools/Map/Bake Tile Positions (Active Scene)")]
    private static void BakeTilePositions()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<Tile> tiles = CollectTiles(scene);
        if (tiles.Count == 0)
        {
            WarnNoTiles();
            return;
        }

        Vector2 origin = FindOrigin(tiles); // 격자 원점(최소 x/z)
        AssignCoords(tiles, origin);        // 각 타일에 Col/Row 새김
        SaveResult(scene, tiles.Count);     // 씬 저장 표시 + 완료 로그
    }

    // 씬 순회로 타일 수집
    private static List<Tile> CollectTiles(Scene scene)
    {
        var tiles = new List<Tile>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            tiles.AddRange(root.GetComponentsInChildren<Tile>(true));
        }
        return tiles;
    }

    // 타일이 하나도 없을 때 사용자에게 안내.
    private static void WarnNoTiles()
    {
        EditorUtility.DisplayDialog("Tile Pos Baker",
            "Tile 컴포넌트를 가진 타일을 찾지 못했습니다. 큐브 프리팹에 Tile을 붙여 씬에 배치한 뒤 다시 실행하세요.",
            "확인");
    }

    // 격자 원점 = 모든 타일의 최소 x/z. 런타임 MapBoard.Build와 같은 기준(min→0)이라 좌표가 일치한다.
    // 반환 Vector2는 (x = minX, y = minZ) — y 칸에 월드 z를 담는다.
    private static Vector2 FindOrigin(List<Tile> tiles)
    {
        float minX = float.MaxValue, minZ = float.MaxValue;
        foreach (Tile tile in tiles)
        {
            minX = Mathf.Min(minX, tile.transform.position.x);
            minZ = Mathf.Min(minZ, tile.transform.position.z);
        }
        return new Vector2(minX, minZ);
    }

    // 각 타일의 월드 위치를 (Col,Row)로 바꿔 State에 저장.
    private static void AssignCoords(List<Tile> tiles, Vector2 origin)
    {
        Undo.SetCurrentGroupName("Bake Tile Positions");
        int group = Undo.GetCurrentGroup();

        foreach (Tile tile in tiles)
        {
            Undo.RecordObject(tile, "Bake Tile Position");
            tile.State ??= new TileState();

            Vector2Int cell = GridCalculator.GetCellFromWorldPos(tile.transform.position, origin.x, origin.y, CellSize);
            tile.State.Col = cell.x; // 월드 x → Col
            tile.State.Row = cell.y; // 월드 z → Row

            EditorUtility.SetDirty(tile);
        }

        Undo.CollapseUndoOperations(group);
    }

    // 씬을 "변경됨"으로 표시하고(저장 대상), 결과를 로그로 알린다.
    private static void SaveResult(Scene scene, int tileCount)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[TilePosBaker] 좌표 베이크 완료 — Tile {tileCount}개.");
    }
}