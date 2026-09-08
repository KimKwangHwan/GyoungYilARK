using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 포토모드 툴이 쓰는 캡처 헬퍼. 두 가지를 뽑는다.
//  1) CaptureScreen  - 현재 게임 카메라를 오프스크린 RT에 렌더 → 스크린스페이스 UI/IMGUI가
//                      카메라를 안 타므로 자동으로 빠진 "UI 없는 게임 화면".
//  2) CaptureCharacters - 전용 카메라로 대상 유닛만(레이어 격리) 검정/흰 배경 두 번 렌더한 뒤
//                      차이로 알파를 복원하고, 불투명 바운딩 박스로 자동 크롭한 투명 PNG
//                      (에디터 EnemyIconBaker의 Unpremultiply 기법 이식).
public static class PhotoCapture
{
    private const string CaptureLayerName = "PhotoCapture";

    private static Camera captureCam;

    // ── 게임 화면 (UI 제외) ────────────────────────────────────────────────
    public static Texture2D CaptureScreen(Camera cam, int scale)
    {
        if (cam == null) { Debug.LogError("[PhotoMode] Camera.main 이 없습니다."); return null; }

        int w = Screen.width * scale;
        int h = Screen.height * scale;

        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    // ── 배경 없는 캐릭터 ──────────────────────────────────────────────────
    public static Texture2D CaptureCharacters(Camera srcCam, IReadOnlyList<Transform> roots, int scale, bool square)
    {
        if (srcCam == null) { Debug.LogError("[PhotoMode] Camera.main 이 없습니다."); return null; }
        if (roots == null || roots.Count == 0) { Debug.LogWarning("[PhotoMode] 캡처할 캐릭터가 없습니다."); return null; }

        int layer = LayerMask.NameToLayer(CaptureLayerName);
        if (layer < 0)
        {
            Debug.LogError($"[PhotoMode] '{CaptureLayerName}' 레이어가 없습니다. ProjectSettings/TagManager 확인.");
            return null;
        }

        // 1. 선택 아웃라인 먼저 제거 (클론이 사라진 뒤에 레이어를 수집해야 파괴 참조가 안 남는다)
        Hero selected = HeroSelectionService.Current;
        if (selected != null) selected.SetSelected(false);

        // 2. 캡처 카메라 - CopyFrom 은 targetTexture/cullingMask/enabled 까지 복사돼 함정이라 안 쓴다
        EnsureCaptureCam();
        Camera cam = captureCam;
        cam.transform.SetPositionAndRotation(srcCam.transform.position, srcCam.transform.rotation);
        cam.orthographic = srcCam.orthographic;
        cam.fieldOfView = srcCam.fieldOfView;
        cam.orthographicSize = srcCam.orthographicSize;
        cam.nearClipPlane = srcCam.nearClipPlane;
        cam.farClipPlane = srcCam.farClipPlane;
        cam.cullingMask = 1 << layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.allowHDR = false;

        // 3. 안개 셰이더 효과 off
        FogController fog = UnityEngine.Object.FindFirstObjectByType<FogController>();
        if (fog != null) fog.SetFogVisible(false);

        // 4. 대상 유닛만 캡처 레이어로 (월드 체력바 등 Canvas 서브트리는 건드리지 않음 - 마스크에서 자동 제외)
        var savedLayers = new List<(GameObject go, int layer)>();
        foreach (Transform root in roots) Relayer(root, layer, savedLayers);

        // 5. 검정/흰 배경 두 번 렌더 → 알파 복원
        int w = Screen.width * scale;
        int h = Screen.height * scale;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;

        Texture2D onBlack = RenderOnce(cam, rt, Color.black, w, h);
        Texture2D onWhite = RenderOnce(cam, rt, Color.white, w, h);

        cam.targetTexture = null;
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);

        Texture2D full = Unpremultiply(onBlack, onWhite, w, h, out RectInt bbox);
        UnityEngine.Object.Destroy(onBlack);
        UnityEngine.Object.Destroy(onWhite);

        // 6. 원복
        foreach (var (go, l) in savedLayers) if (go != null) go.layer = l;
        if (fog != null) fog.SetFogVisible(true);
        if (selected != null) selected.SetSelected(true);

        // 디버그 (크롭 전) - 궤도 카메라라 캐릭터가 화면 중앙이 아닐 수 있으므로 bbox 중심을 샘플링
        Color corner = full.GetPixel(0, 0);
        int bx = bbox.width > 0 ? bbox.xMin + bbox.width / 2 : w / 2;
        int by = bbox.height > 0 ? bbox.yMin + bbox.height / 2 : h / 2;
        Color mid = full.GetPixel(bx, by);
        Debug.Log($"[PhotoMode] 캐릭터 캡처 알파 - 모서리 {corner.a:F2} (≈0 기대), bbox중심 {mid.a:F2} (≈1 기대), bbox {bbox.width}×{bbox.height}");

        // 7. 불투명 바운딩 박스 + 여백으로 크롭
        Texture2D result = AutoCrop(full, bbox, square);
        if (result != full) UnityEngine.Object.Destroy(full);

        Debug.Log($"[PhotoMode] 캐릭터 캡처 최종 크기 {result.width}×{result.height}");
        return result;
    }

    // ── 저장 ─────────────────────────────────────────────────────────────
    public static string SavePng(Texture2D tex, string prefix)
    {
        if (tex == null) return null;
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "Portfolio");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[PhotoMode] 저장: {path}");
            return path;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PhotoMode] 저장 실패 - {e.Message}");
            return null;
        }
        finally
        {
            UnityEngine.Object.Destroy(tex);
        }
    }

    public static string PortfolioFolder => Path.Combine(Application.persistentDataPath, "Portfolio");

    // ── 내부 ─────────────────────────────────────────────────────────────
    private static void EnsureCaptureCam()
    {
        if (captureCam != null) return;

        var go = new GameObject("[PhotoModeCaptureCam]") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.Object.DontDestroyOnLoad(go);

        captureCam = go.AddComponent<Camera>();
        captureCam.enabled = false; // 매 프레임 자동 렌더 금지 - 캡처 때 수동 Render()

        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderType = CameraRenderType.Base;
        data.renderPostProcessing = false; // 포스트가 켜지면 알파를 1로 덮어씀 (PC_RPAsset.m_AllowPostProcessAlphaOutput=0)
        data.renderShadows = true;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
    }

    private static Texture2D RenderOnce(Camera cam, RenderTexture rt, Color bg, int w, int h)
    {
        cam.backgroundColor = new Color(bg.r, bg.g, bg.b, 1f);
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        return tex;
    }

    // 검정 배경 결과와 흰 배경 결과의 차이가 곧 "배경이 비쳐 보인 정도" = 투명도.
    // alpha 로 안 나눠주면(프리멀티플라이) 반투명 가장자리가 검게 죽는다.
    // 같은 루프에서 불투명(alpha>0.01) 픽셀의 바운딩 박스도 계산한다(재스캔 방지).
    private static Texture2D Unpremultiply(Texture2D onBlack, Texture2D onWhite, int w, int h, out RectInt bbox)
    {
        Color[] black = onBlack.GetPixels();
        Color[] white = onWhite.GetPixels();
        var outPixels = new Color[black.Length];

        int minX = w, minY = h, maxX = -1, maxY = -1;

        for (int i = 0; i < black.Length; i++)
        {
            Color b = black[i];
            Color wh = white[i];

            float diff = Mathf.Max(wh.r - b.r, Mathf.Max(wh.g - b.g, wh.b - b.b));
            float alpha = Mathf.Clamp01(1f - diff);

            if (alpha <= 0.001f)
            {
                outPixels[i] = Color.clear;
                continue;
            }

            outPixels[i] = new Color(b.r / alpha, b.g / alpha, b.b / alpha, alpha);

            if (alpha > 0.01f)
            {
                int x = i % w;
                int y = i / w;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        bbox = maxX < 0
            ? new RectInt(0, 0, 0, 0)
            : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);

        var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
        result.SetPixels(outPixels);
        result.Apply();
        return result;
    }

    // 불투명 바운딩 박스에 여백을 더해 잘라낸다. square=true면 중심 유지하며 정사각형으로 확장.
    private static Texture2D AutoCrop(Texture2D full, RectInt bbox, bool square)
    {
        if (bbox.width <= 0 || bbox.height <= 0)
        {
            Debug.LogWarning("[PhotoMode] 불투명 픽셀이 없어 크롭을 건너뜁니다.");
            return full;
        }

        int w = full.width;
        int h = full.height;

        int pad = Mathf.Max(16, Mathf.RoundToInt(Mathf.Max(bbox.width, bbox.height) * 0.05f));
        int x0 = bbox.xMin - pad;
        int y0 = bbox.yMin - pad;
        int x1 = bbox.xMax + pad;
        int y1 = bbox.yMax + pad;

        if (square)
        {
            int cx = (x0 + x1) / 2;
            int cy = (y0 + y1) / 2;
            int half = Mathf.Max(x1 - x0, y1 - y0) / 2 + 1;
            x0 = cx - half; x1 = cx + half;
            y0 = cy - half; y1 = cy + half;
        }

        x0 = Mathf.Clamp(x0, 0, w - 1);
        y0 = Mathf.Clamp(y0, 0, h - 1);
        x1 = Mathf.Clamp(x1, x0 + 1, w);
        y1 = Mathf.Clamp(y1, y0 + 1, h);

        int cw = x1 - x0;
        int ch = y1 - y0;

        var cropped = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        cropped.SetPixels(full.GetPixels(x0, y0, cw, ch));
        cropped.Apply();
        return cropped;
    }

    private static void Relayer(Transform t, int layer, List<(GameObject, int)> saved)
    {
        if (t == null || !t.gameObject.activeSelf) return;
        if (t.GetComponent<Canvas>() != null) return; // 월드 체력바 등 - 캡처 마스크(캡처 레이어 전용)에서 이미 빠진다

        saved.Add((t.gameObject, t.gameObject.layer));
        t.gameObject.layer = layer;

        for (int i = 0; i < t.childCount; i++)
            Relayer(t.GetChild(i), layer, saved);
    }
}
