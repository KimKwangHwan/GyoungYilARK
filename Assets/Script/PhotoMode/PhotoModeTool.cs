using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 포트폴리오/홍보용 인게임 촬영 툴. F12 로 포토모드를 토글한다.
//  - 진입 시: 게임 정지(Time.timeScale=0), HUD/체력바 숨김, 게임플레이 카메라 조작(CameraInput)을 끄고
//    대상 중심 궤도 카메라로 인계한다. → 게임플레이 탑뷰 리그로는 불가능한 "캐릭터 정면" 촬영이 가능.
//  - 우상단 IMGUI 패널: 카메라 프리셋(정면/측면/후면/45°/눈높이/대상 채우기), "화면 저장",
//    "캐릭터 저장(투명)", "폴더 열기".
//  - CameraFreeLook 과 같은 결의 독립 씬 툴 - DI 미사용, RuntimeInitializeOnLoadMethod 로 자동 부트스트랩.
// MainScene 에서만 동작한다.
public class PhotoModeTool : MonoBehaviour
{
    private const string GameplaySceneName = "MainScene";
    private const int Scale = 2;          // 캡처 해상도 = 창 크기 ×2
    private const float LookSens = 0.2f;  // 궤도 회전 감도(픽셀당 도)
    private const float PanSpeed = 12f;   // WASD/QE 초점 이동 속도(월드 단위/초)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[PhotoModeTool]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        go.AddComponent<PhotoModeTool>();
    }

    private InputAction toggleAction;
    private bool active;
    private bool capturing;

    private float savedTimeScale;

    // 카메라 인계
    private Camera mainCam;
    private CameraInput mainCamInput;
    private CameraRig rig;
    private float savedMinDistance;
    private float savedMinPitch;

    // 궤도 상태
    private Vector3 orbitFocus;
    private float orbitYaw;
    private float orbitPitch;
    private float orbitDist;

    // HUD 원복
    private readonly List<(CanvasGroup group, float alpha, bool interactable, bool blocksRaycasts)> hudGroups = new();
    private readonly List<Canvas> worldCanvases = new();
    private readonly List<Transform> rootBuffer = new();

    private bool targetAllUnits;
    private bool cropSquare;
    private string lastSaveInfo;

    private void OnEnable()
    {
        toggleAction = new InputAction("PhotoModeToggle", binding: "<Keyboard>/f12");
        toggleAction.performed += OnToggle;
        toggleAction.Enable();
    }

    private void OnDisable()
    {
        toggleAction.performed -= OnToggle;
        toggleAction.Disable();
        toggleAction.Dispose();
        if (active) Exit();
    }

    private void OnToggle(InputAction.CallbackContext _)
    {
        if (TutorialInputGate.BlockHotkeys) return;
        if (active) Exit();
        else Enter();
    }

    private void Update()
    {
        if (!active) return;

        if (SceneManager.GetActiveScene().name != GameplaySceneName)
        {
            Exit();
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Exit();
            return;
        }

        if (!capturing) OrbitInput();
        ApplyOrbit();
    }

    // ── 진입 / 종료 ──────────────────────────────────────────────────────
    private void Enter()
    {
        if (SceneManager.GetActiveScene().name != GameplaySceneName) return;

        mainCam = Camera.main;
        if (mainCam == null) { Debug.LogError("[PhotoMode] Camera.main 이 없습니다."); return; }

        active = true;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        HideHud();

        mainCamInput = mainCam.GetComponent<CameraInput>();
        if (mainCamInput != null) mainCamInput.enabled = false;

        rig = mainCam.GetComponent<CameraRig>();
        if (rig != null)
        {
            rig.SuspendClamp();
            savedMinDistance = rig.minDistance;
            savedMinPitch = rig.minPitch;
            rig.minDistance = 0.5f;
            rig.minPitch = -85f;
        }

        SeedOrbitFromCurrentView();
    }

    private void Exit()
    {
        if (mainCamInput != null) mainCamInput.enabled = true;
        if (rig != null)
        {
            rig.minDistance = savedMinDistance;
            rig.minPitch = savedMinPitch;
            rig.ResumeClamp();
            rig.ApplyNow(); // 게임플레이 시점으로 복귀
        }
        mainCam = null;
        mainCamInput = null;
        rig = null;

        ShowHud();
        Time.timeScale = savedTimeScale;
        active = false;
    }

    // ── 궤도 카메라 ─────────────────────────────────────────────────────
    private void SeedOrbitFromCurrentView()
    {
        orbitFocus = rig != null ? rig.focus : mainCam.transform.position + mainCam.transform.forward * 20f;

        Vector3 toCam = mainCam.transform.position - orbitFocus;
        orbitDist = Mathf.Max(toCam.magnitude, 1f);

        Quaternion look = toCam.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation((-toCam).normalized, Vector3.up)
            : mainCam.transform.rotation;
        Vector3 e = look.eulerAngles;
        orbitYaw = e.y;
        orbitPitch = NormalizePitch(e.x);
    }

    private static float NormalizePitch(float euler)
    {
        if (euler > 180f) euler -= 360f;
        return Mathf.Clamp(euler, -85f, 85f);
    }

    private void OrbitInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue();

            // RMB 드래그 = 궤도 회전 (LMB 는 히어로 선택용으로 남겨둔다)
            if (mouse.rightButton.isPressed && d != Vector2.zero)
            {
                orbitYaw += d.x * LookSens;
                orbitPitch = Mathf.Clamp(orbitPitch - d.y * LookSens, -85f, 85f);
            }

            // MMB 드래그 = 초점 화면평면 이동
            if (mouse.middleButton.isPressed && d != Vector2.zero)
            {
                Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
                Vector3 right = rot * Vector3.right;
                Vector3 up = rot * Vector3.up;
                orbitFocus += (-right * d.x - up * d.y) * (orbitDist * 0.0015f);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                orbitDist = Mathf.Clamp(orbitDist * (1f - Mathf.Sign(scroll) * 0.1f), 0.5f, 200f);
        }

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            Quaternion flat = Quaternion.Euler(0f, orbitYaw, 0f);
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += flat * Vector3.forward;
            if (kb.sKey.isPressed) move -= flat * Vector3.forward;
            if (kb.dKey.isPressed) move += flat * Vector3.right;
            if (kb.aKey.isPressed) move -= flat * Vector3.right;
            if (kb.eKey.isPressed) move += Vector3.up;
            if (kb.qKey.isPressed) move -= Vector3.up;
            if (move != Vector3.zero)
                orbitFocus += move.normalized * (PanSpeed * Time.unscaledDeltaTime);
        }
    }

    private void ApplyOrbit()
    {
        if (mainCam == null) return;
        Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        mainCam.transform.SetPositionAndRotation(orbitFocus - rot * Vector3.forward * orbitDist, rot);
    }

    // 대상이 바라보는 방향(수평). 단일 히어로면 그 forward, 아니면 world -Z.
    private Vector3 GetTargetFacing()
    {
        Hero hero = HeroSelectionService.Current;
        if (!targetAllUnits && hero != null)
        {
            Vector3 f = hero.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.0001f) return f.normalized;
        }
        return Vector3.back;
    }

    // yawOffset: 대상 정면(카메라가 대상의 앞면을 봄) 기준 추가 회전. 0=정면, 180=후면, ±90=측면.
    private void ApplyCameraPreset(float yawOffset, float? pitch = null)
    {
        Vector3 facing = GetTargetFacing();
        float frontYaw = Mathf.Atan2(-facing.x, -facing.z) * Mathf.Rad2Deg;
        orbitYaw = frontYaw + yawOffset;
        if (pitch.HasValue) orbitPitch = pitch.Value;

        if (TryGetTargetBounds(out Bounds b)) orbitFocus = b.center;
        ApplyOrbit();
    }

    private void FitTarget()
    {
        if (!TryGetTargetBounds(out Bounds b)) return;
        orbitFocus = b.center;
        float radius = Mathf.Max(b.extents.magnitude, 0.001f);
        float fov = mainCam != null ? mainCam.fieldOfView : 30f;
        orbitDist = radius / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.1f;
        ApplyOrbit();
    }

    private bool TryGetTargetBounds(out Bounds bounds)
    {
        bounds = default;
        bool has = false;
        foreach (Transform t in CollectRoots())
        {
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
        }
        return has;
    }

    // ── HUD ────────────────────────────────────────────────────────────
    private void HideHud()
    {
        hudGroups.Clear();
        worldCanvases.Clear();

        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas) continue;

            if (c.renderMode == RenderMode.WorldSpace)
            {
                worldCanvases.Add(c);
                c.enabled = false;
            }
            else
            {
                CanvasGroup g = c.GetComponent<CanvasGroup>();
                if (g == null) g = c.gameObject.AddComponent<CanvasGroup>();
                hudGroups.Add((g, g.alpha, g.interactable, g.blocksRaycasts));
                g.alpha = 0f;
                g.interactable = false;
                g.blocksRaycasts = false;
            }
        }
    }

    private void ShowHud()
    {
        foreach (var (g, alpha, interactable, blocksRaycasts) in hudGroups)
        {
            if (g == null) continue;
            g.alpha = alpha;
            g.interactable = interactable;
            g.blocksRaycasts = blocksRaycasts;
        }
        foreach (Canvas c in worldCanvases)
            if (c != null) c.enabled = true;

        hudGroups.Clear();
        worldCanvases.Clear();
    }

    // ── 대상 수집 / 캡처 ────────────────────────────────────────────────
    private IReadOnlyList<Transform> CollectRoots()
    {
        rootBuffer.Clear();

        if (targetAllUnits)
        {
            foreach (Hero hero in FindObjectsByType<Hero>(FindObjectsSortMode.None))
                if (IsVisible(hero)) rootBuffer.Add(hero.transform);
            foreach (EnemyBase enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
                if (IsVisible(enemy)) rootBuffer.Add(enemy.transform);
        }
        else
        {
            Hero selected = HeroSelectionService.Current;
            if (selected != null) rootBuffer.Add(selected.transform);
        }

        return rootBuffer;
    }

    private static bool IsVisible(Component unit)
    {
        foreach (Renderer r in unit.GetComponentsInChildren<Renderer>())
            if (r.isVisible) return true;
        return false;
    }

    // 프레임 렌더가 끝난 뒤 캡처한다 - OnGUI 도중 Camera.Render()를 부르면 SRP 재귀 렌더 경고가 난다.
    // WaitForEndOfFrame 은 Time.timeScale=0 에서도 정상 동작한다.
    private IEnumerator CaptureRoutine(bool characters)
    {
        capturing = true;
        yield return new WaitForEndOfFrame();

        Texture2D shot = characters
            ? PhotoCapture.CaptureCharacters(mainCam, CollectRoots(), Scale, cropSquare)
            : PhotoCapture.CaptureScreen(mainCam, Scale);

        int cw = shot != null ? shot.width : 0;
        int ch = shot != null ? shot.height : 0;
        string path = PhotoCapture.SavePng(shot, characters ? "Character" : "Screen");
        if (path != null) lastSaveInfo = $"{Path.GetFileName(path)}  ({cw}×{ch})";

        capturing = false;
    }

    // ── IMGUI 패널 ─────────────────────────────────────────────────────
    private void OnGUI()
    {
        if (!active) return;

        GUI.skin.label.richText = true;
        const float width = 260f;
        float x = Screen.width - width - 12f;

        GUILayout.BeginArea(new Rect(x, 12f, width, 430f), GUI.skin.box);

        GUILayout.Label("<b>포토 모드</b>  (F12 / ESC 종료)");
        GUILayout.Label("<i>RMB 회전 · 휠 줌 · MMB/WASD·QE 이동</i>");
        GUILayout.Space(6f);

        GUI.enabled = !capturing;

        GUILayout.Label("카메라 프리셋:");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("정면")) ApplyCameraPreset(0f, 8f);
        if (GUILayout.Button("후면")) ApplyCameraPreset(180f, 8f);
        if (GUILayout.Button("좌")) ApplyCameraPreset(90f, 8f);
        if (GUILayout.Button("우")) ApplyCameraPreset(-90f, 8f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("45°")) ApplyCameraPreset(45f, 12f);
        if (GUILayout.Button("눈높이")) { orbitPitch = 8f; ApplyOrbit(); }
        if (GUILayout.Button("대상 채우기")) FitTarget();
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        if (GUILayout.Button("화면 저장  (UI 없음, ×2)"))
            StartCoroutine(CaptureRoutine(characters: false));

        GUILayout.Space(8f);
        GUILayout.Label("배경 없는 캐릭터:");
        targetAllUnits = GUILayout.Toggle(targetAllUnits,
            targetAllUnits ? "  화면 속 모든 유닛" : "  선택한 히어로만");
        cropSquare = GUILayout.Toggle(cropSquare, "  정사각형으로 크롭");

        bool canCapture = !capturing && (targetAllUnits || HeroSelectionService.Current != null);
        GUI.enabled = canCapture;
        if (GUILayout.Button("캐릭터 저장  (투명 PNG, ×2)"))
            StartCoroutine(CaptureRoutine(characters: true));
        GUI.enabled = !capturing;
        if (!canCapture && !capturing) GUILayout.Label("<i>히어로를 클릭해 선택하세요</i>");

        GUILayout.Space(10f);
        if (GUILayout.Button("저장 폴더 열기"))
            Application.OpenURL(PhotoCapture.PortfolioFolder);

        GUI.enabled = true;

        if (!string.IsNullOrEmpty(lastSaveInfo))
        {
            GUILayout.Space(6f);
            GUILayout.Label("최근 저장:\n" + lastSaveInfo);
        }

        GUILayout.EndArea();
    }
}
