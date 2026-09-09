using UnityEngine;

// 카메라 뷰포트 밖(마진 포함)의 순수 연출용(VFX/파티클) 이펙트를 다루기 위한 유틸. IsOffscreen은
// 스폰 시점 위치 판정(1회성·단명 이펙트의 인스턴스화 자체를 건너뛰는 용도), SetVisualActive는 이미
// 스폰돼 계속 살아있는 인스턴스의 렌더링/시뮬레이션을 매 프레임 켜고 끄는 용도다. EnemySoundManager.
// IsOffscreen과 동일한 뷰포트 투영 판정을, MonoBehaviour 인스턴스가 없는 호출부(Hero)에서도 쓸 수
// 있게 정적 클래스로 옮겨 둔 것.
//
// IsOffscreen을 스폰 생략 용도로 쓸 때는 절대 게임플레이(피해/디버프/힐/장판 틱)를 소유하는 스폰
// 경로에 쓰지 말 것 — 스폰된 GameObject 자체가 TakeDamage/Heal/버프 적용의 주체인 경우(GroundZoneEffect,
// Projectile.Launch 등)는 대상 제외. 그런 경우엔 대신 Hero.SpawnPersistentEffectAlways로 무조건
// 스폰하고, SetVisualActive로 매 프레임 가시성만 별도로 관리한다.
public static class VfxVisibility
{
    public const float DefaultMargin = 0.3f;

    // 문제 진단(A/B)용 전역 스위치. 끄면 아무것도 컷하지 않는다.
    public static bool Enabled = true;

    private static Camera mainCam;
    // 캐시가 파괴됐거나 비활성(씬 전환/낮밤 카메라 교체 등)이면 다시 찾는다 — 한 번 엉뚱한
    // 카메라를 물면 WorldToViewportPoint가 항상 화면 밖으로 나와 이펙트가 통째로 안 보이게 된다.
    private static Camera MainCam =>
        mainCam != null && mainCam.isActiveAndEnabled ? mainCam : (mainCam = Camera.main);

    // 카메라가 오빗+줌(distance 5~120, pitch 5~89°)이라 절대 거리로는 판정할 수 없어 뷰포트로
    // 투영한다 — 줌/팬/피치/종횡비가 전부 반영된다. 탑다운이라 높낮이도 투영이 알아서 처리한다.
    public static bool IsOffscreen(Vector3 worldPos, float margin = DefaultMargin)
    {
        if (!Enabled) return false;
        Camera cam = MainCam;
        // 카메라를 못 찾으면 컷하지 않는다(fail-open) — 판정 불가 상황에서 안 보이게 되는 것보다
        // 한 번 더 스폰되는 게 낫다. EnemySoundManager.IsOffscreen과 동일한 원칙.
        if (cam == null) return false;

        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z < 0f) return true; // 카메라 뒤
        return vp.x < -margin || vp.x > 1f + margin
            || vp.y < -margin || vp.y > 1f + margin;
    }

    // 이미 스폰된(계속 추적 중인) 인스턴스를 파괴하지 않고 화면 밖일 때만 렌더링/시뮬레이션을 끈다.
    // 렌더러는 enabled로 즉시 안 보이게, 파티클은 Pause/Play로 시뮬레이션 자체를 멈춰 최적화 취지를
    // 살린다. 게임플레이 로직(콜라이더/이동/틱)은 건드리지 않으므로 피격 판정과는 무관하다.
    //
    // 멱등하게 동작한다 — 이미 원하는 상태면 아무것도 하지 않는다. 호출부가 매 프레임 불러도
    // 안전하도록(특히 active==true를 반복 호출해도 재생 중인 1회성 파티클을 매 프레임 되감지
    // 않도록: Play는 '일시정지 상태'일 때만 건다).
    public static void SetVisualActive(GameObject instance, bool active)
    {
        if (instance == null) return;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            if (r.enabled != active) r.enabled = active;
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (active) { if (ps.isPaused) ps.Play(true); }
            else { if (!ps.isPaused && !ps.isStopped) ps.Pause(true); }
        }
    }
}
