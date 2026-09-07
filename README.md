# PioneerOfFelucia

> 낮에는 마을을 세우고 영웅을 배치하며, 밤에는 몰려오는 적 웨이브로부터 본진을 지키는
> **베이스 빌딩 + 그리드 타워 디펜스** 하이브리드 게임 (모바일).
> 저장소명은 개발 초기 코드네임인 `GyoungYilARK`이며, 프로덕트명은 `PioneerOfFelucia`입니다.

![Unity](https://img.shields.io/badge/Unity-6000.3.15f1-black?logo=unity)
![URP](https://img.shields.io/badge/Render-URP%2017.3-blue)
![Language](https://img.shields.io/badge/Language-C%23-239120?logo=csharp)
![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20iOS-brightgreen)

<!-- 게임플레이 스크린샷 / GIF 은 추후 첨부 예정 -->
<!--
| 낮 (건설·영웅 배치) | 밤 (웨이브 방어) |
| :---: | :---: |
| ![day](docs/day.png) | ![night](docs/night.png) |
-->

---

## 프로젝트 개요

| 항목 | 내용 |
| --- | --- |
| 장르 | 베이스 빌딩 + 그리드 타워 디펜스 (Base-building + Grid Tower Defense) |
| 플랫폼 | 모바일 (Android / iOS), 세로 화면 |
| 엔진 | Unity `6000.3.15f1` (Unity 6.3) / URP `17.3` |
| 팀 규모 | 4인 (프로그래머) |
| 개발 기간 | 2026.07 ~ 2026.09 |
| 담당 | 김광환 — 영웅 전투 · 공격 데이터 시스템 · VFX · 스킬 UI (저장소 소유자 / 최다 기여자) |

### 핵심 게임 루프

- 🌞 **낮 (Day)** — 경제 건물을 짓고 자원을 모으며, 영웅을 뽑고 합성해 타일 그리드에 배치·재배치하고 업그레이드한다.
- 🌙 **밤 (Night)** — 스폰 타일에서 적 웨이브가 등장해 A\* 경로를 따라 본진(Core)까지 침투한다. 배치된 영웅이 사거리 안의 적과 자동 전투한다.
- 낮/밤이 반복되며 날짜(day)가 올라갈수록 웨이브가 강해지고, 본진 HP 가 0 이 되면 게임 오버.

---

## 게임플레이

- **영웅** — 7개 클래스(SwordMan / Archer / SpearMan / DualSwordMan / Mage / THS / Healer) × 4티어. 같은 영웅을 합성해 상위 티어로, 티어·클래스·스탯 업그레이드로 성장. 현재 29종의 영웅 데이터가 구현되어 있다.
- **공격 패턴** — 영웅마다 단일 / 범위(AoE) / 체인(chain) / 라인(line) / 멀티샷 / 채널링(빔·회전 베기) 등 서로 다른 공격을 데이터로 정의. 트레잇(오라·피흡·N타 강공 등)과 액티브 스킬을 조합.
- **적** — 근접 / 원거리, 일반 / 정예 / 보스. 9종의 특성(비행·은신·저지불가·폭주·재생·타수 보호막·잠행·수영·화염)이 비트 플래그로 조합된다.
- **기믹 타일** — 물 / 불 / 모래바람 / 바람벽 등 지형 타일이 적·영웅과 상호작용(예: 수영 적만 물을 건너고, 화염족은 불 타일에서 강해진다).
- **본진 방어** — 침투에 성공한 적 등급에 따라 본진 HP 가 감소.

---

## 기술 스택

| 영역 | 사용 기술 |
| --- | --- |
| DI 컨테이너 | [VContainer](https://github.com/hadashiA/VContainer) 1.18 |
| 비동기 | [UniTask](https://github.com/Cysharp/UniTask) |
| 경로 탐색 | 타일 이웃 그래프 기반 커스텀 A\* (이진 힙 open-set) |
| 데이터 | CSV → `DataTable` 파이프라인 + 런타임 로컬라이즈 |
| 상태 관리 | 게임/영웅 FSM (`Day`·`Night`·`Result`·`GameOver`) |
| 최적화 | 오브젝트 풀링, 화면 밖 VFX 컬링 |
| 저장 | 암호화 세이브/로드, 결정론적 시드(seed) 기반 재현 |
| 패키지 관리 | UPM (git 의존성) + NuGetForUnity |

---

## 아키텍처 하이라이트 (팀 공통)

### 커스텀 타일 보드 & A\* 길찾기 · *김지훈*
`Assets/Script/Map/Core/` — `MapBoard` / `Tile` / `Pathfinder` / `TileHeap`

- 콜라이더가 아닌 **타일 이웃 그래프**로 이동·사거리·점유를 판정. `MapBoard.Build()` 시점에 각 타일→최근접 본진 칸 거리(`_coreDistance`)를 미리 계산해 A\* 휴리스틱으로 재사용.
- `open-set` 은 이진 힙(`TileHeap`)으로 관리해 매 스텝 최솟값 선형 탐색을 제거.
- `CellFromRay()` — 타일을 윗면 한 장이 아니라 바닥까지 이어진 **기둥(column slab)** 으로 보고 레이와 교차. broad-phase 바운즈 컬링 + 보드 로컬 좌표계 변환으로 **맵이 회전돼 있어도** 정확히 클릭 판정.

### DI 구성 · *최진우 / 김지훈*
`Assets/GameLoop(ChoiJinWoo)/Scripts/VContainer/GameLifeTimeScope.cs`

- 매니저·세이브 계층·풀 매니저를 VContainer 로 등록. lazy 등록으로 인해 생성되지 않는 싱글턴은 `RegisterBuildCallback` 에서 강제 `Resolve` 해 초기화 순서를 보장.

### 데이터 주도 (Data-driven) · *남상준*
`Assets/Script/DataTable/`

- 영웅·적·웨이브·스킬·로컬라이즈 텍스트를 CSV 로 관리하고 임포트. 밸런싱을 코드 수정 없이 반복.

### 적 & 웨이브 · *남상준*
`Assets/Script/Enemy/` — `WaveSpawner` / `EnemyBase`

- `MapBoard.GetWaypoints()` 로 계산된 스폰→본진 경로를 따라 이동, `PoolManager` 로 풀링.

---

## 담당 파트 — 김광환

> 파일 최초 작성자(`git log --diff-filter=A`)와 커밋 지분으로 확인 가능한 범위입니다.

### 1. 데이터 주도 공격 시스템 (Data-driven Attack System)

`Assets/Script/Hero/AttackDataSO/` — 약 26개 스크립트, ~51개 AttackData 에셋

하나의 `AttackDataSO`(ScriptableObject)로 **한 영웅의 공격 한 종류를 통째로 저작**한다.
탐지 범위·형태 / 타격 형태(단일·범위·체인·라인) / 멀티샷·타겟 분배 / 타이밍(Discrete vs Continuous 채널링) / 버프·디버프·장판(ground zone)·이펙트·사운드까지 모두 필드로 노출하고, `OnValidate()` 로 잘못된 조합을 에디터에서 경고한다.

배달과 실행을 두 축의 전략 패턴으로 분리했다.

```csharp
// 타이밍 축: 어떻게 "발동"되는가
public interface IAttackDeliveryStrategy { ... }
//  ├─ DiscreteAttackStrategy      : 애니메이션 이벤트 윈도우 기반 1회 집행
//  └─ ContinuousBeamStrategy      : tick 주기로 채널링 (Beam / ProjectileVolley / SelfArea)

// 전투 타입 축: 무엇이 "때리는가" — {SwordMan,Archer,Mage,Healer}AttackState 가 주입
public interface IAttackExecutor { ... }
//  ├─ MeleeAttackExecutor
//  ├─ RangedAttackExecutor        : 투사체 발사 (근접 영웅도 데이터로 원거리화 가능)
//  └─ HealAttackExecutor          : 아군 대상
```

- `ChainResolver` — 체인 공격의 튕김 대상 선정 + falloff, `BeamLinkEffect` 로 두 지점 연결 이펙트 추적.
- `AttackTargetSelector` — 멀티샷의 대상 분배(순회 순서 안정성 보장).
- `AttackDamageUtil` — 데미지·크리·장판·디버프 적용을 한곳으로 모은 계산 유틸.
- `AttackAnimSpeedUtil` — 공격 속도 스탯에 맞춰 애니메이션 클립 배속을 정규화.

관련: `Hero.cs`, 클래스별 `Archer` / `Mage` / `Healer` / `SwordMan`, 영웅 FSM(`Assets/Script/Hero/HeroState/`).

### 2. 트레잇 · 합성 · 업그레이드

- **트레잇** `Assets/Script/Hero/HeroTrait/` — `OnAttackPerformed` / `OnHit` / `OnKill` / `OnDayStart` 등 훅에 반응하는 컴포넌트 10종(오라, 피흡, N타 강공, 확률 재발동, 스택형 최대치, 스탠스 전환 등).
- **액티브 스킬** `HeroActiveSkill` + `HeroSkillCastController` — 플레이어 클릭 → 타일 타겟 → 채널링/즉발 분기, `CancellationToken` 으로 사망·디스폰 시 안전하게 취소.
- **합성** `HeroCombineManager` — 드래그 또는 맵 위 영웅 더블클릭으로 동일 영웅 합성, 티어 1→4 승급.
- **업그레이드** `HeroStatManager` + 티어/클래스/스탯 업그레이드 — 업그레이드 결과를 `HeroData` 단위로 캐싱하고, 진행 중인 버프 Modifier 를 유지한 채 base 스탯만 갱신.
- 영웅 도감(archive) UI + 필드 스탯 툴팁.

### 3. VFX 파이프라인 & 파티클 최적화

`features/520-particle_system_optimization`

- `Assets/Script/Common/VfxVisibility.cs` — 순수 연출용 이펙트는 **화면 밖(마진 포함)이면 인스턴스화 자체를 생략**하고, 계속 살아있어야 하는 이펙트는 매 프레임 렌더러/파티클 가시성만 토글.
- 이펙트·투사체 풀을 **영웅 인스턴스가 소유** — 씬 언로드로 영웅이 파괴되면 풀도 함께 사라져 dangling 참조가 없다.
- `GroundZoneEffect`(장판) — 프리팹을 풀에서 꺼내 위치만 잡아주면 데미지/힐 tick·소멸·풀 반납을 컴포넌트가 스스로 처리. 착탄 높이가 공중이어도 바닥 타일 높이로 스냅.
- `BeamLinkEffect` — 빔/체인 이펙트가 움직이는 대상을 매 프레임 따라가도록 끝점 추적.

### 4. 에디터 툴링

`Assets/Script/Editor/`, `Assets/Script/Hero/Editor/`

- `HeroTableImporter` / `HeroStatTableImporter` — CSV → 영웅/스탯 ScriptableObject 일괄 생성·갱신.
- `AttackDataSOEditor` — AttackData 인스펙터 커스터마이즈.
- 치트/시뮬레이션 뷰 — `DayFlowCheatView`, `HeroUpgradeCheatView`, `BaseStateCheatView`, `ResourceGrantView` 등으로 밤/낮 흐름·업그레이드·자원을 즉시 조작해 테스트.
- 데이터 스키마 변경 시 기존 에셋을 일괄 변환하는 마이그레이션 스크립트(`AttackDataMigration`, `HeroEffectLifetimeMigration`).

### 5. 플레이어 스킬 UI · 기타

- 플레이어 액티브 스킬 패널 `PlayerSkillPanel`, 방사형 토글 `SkillRadialToggle`, 마나바.
- 패널 등장 애니메이션(`PanelPopIn` / `PanelReveal`), 버튼 SFX 자동 부착 시스템(`ButtonSfxAutoAttacher`).
- 타이틀 씬, 게임 아이콘, 사운드 매니저, 로컬라이즈 CSV 텍스트.

---

## 팀 & 역할

| 팀원 | 담당 영역 |
| --- | --- |
| **김광환** (louie1004) | 영웅 전투 시스템, 데이터 주도 공격/스킬/트레잇, 합성·업그레이드, VFX·파티클 최적화, 스킬 UI, 타이틀 |
| **김지훈** | 타일 맵 보드, A\* 길찾기, 맵 모듈/기믹 타일, 세이브·로드 |
| **남상준** | 적 AI·웨이브 스포너, CSV 데이터테이블 파이프라인, 적 특성·사운드 |
| **최진우** | 게임 루프(FSM), 경제·생산 시설, 시민, VContainer DI 구성 |

---

## 빌드 / 실행

1. **Unity `6000.3.15f1`** 로 프로젝트를 연다 (Unity 6.3).
2. `Packages/manifest.json` 의 git 의존성(UniTask, VContainer, NuGetForUnity, MCP for Unity)은 최초 오픈 시 자동으로 복원된다.
3. 진입 씬: `Assets/MainScene/Title.unity`
4. 대상 플랫폼: Android / iOS (에디터 플레이로도 전체 루프 테스트 가능).
