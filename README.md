# PioneerOfFelucia

> 낮에는 마을을 세우고 영웅을 배치하며, 밤에는 몰려오는 적 웨이브로부터 본진을 지키는
> **베이스 빌딩 + 그리드 타워 디펜스(Base-building + Grid Tower Defense)** 하이브리드 게임.

![Unity](https://img.shields.io/badge/Unity-6000.3.15f1-black?logo=unity)
![URP](https://img.shields.io/badge/Render-URP%2017.3-blue)
![Language](https://img.shields.io/badge/Language-C%23-239120?logo=csharp)
![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20iOS-brightgreen)

<sup>저장소명 `GyoungYilARK` 는 개발 초기 코드네임이며, 프로덕트명은 `PioneerOfFelucia` 입니다.</sup>

<!-- 게임플레이 스크린샷 / GIF 은 추후 첨부 예정
| 낮 (건설·영웅 배치) | 밤 (웨이브 방어) |
| :---: | :---: |
| ![day](docs/day.png) | ![night](docs/night.png) |
-->

---

## 프로젝트 개요

| 항목 | 내용 |
| --- | --- |
| 장르 | 베이스 빌딩 + 그리드 타워 디펜스 하이브리드 |
| 플랫폼 | 모바일 (Android / iOS), 세로 화면 |
| 엔진 | Unity `6000.3.15f1` (Unity 6.3) / URP `17.3` |
| 개발 기간 | 2026.07 ~ 2026.09 |
| 진입 씬 | `Assets/MainScene/Title.unity` → `Assets/MainScene/MainScene.unity` |

### 핵심 게임 루프

- 🌞 **낮 (Day)** — 지역(region)에 경제 건물을 지어 자원을 모으고, 영웅을 뽑고 합성해 타일 그리드에 배치·재배치하며, 티어·클래스·스탯을 업그레이드한다.
- 🌙 **밤 (Night)** — 스폰 타일에서 적 웨이브가 등장해 A\* 경로를 따라 본진(Core)까지 침투한다. 배치된 영웅은 사거리 안의 적과 자동 전투한다.
- 낮/밤이 반복되며 날짜가 올라갈수록 웨이브가 강해지고, 본진 HP 가 0 이 되면 게임 오버. 진행 상황은 페이즈마다 자동 저장된다.

---

## 게임플레이

- **영웅** — 7개 클래스(SwordMan / Archer / SpearMan / DualSwordMan / Mage / THS / Healer) × 4티어. 같은 영웅을 합성해 상위 티어로 승급하고, 데이터는 현재 29종이 구현되어 있다.
- **공격 패턴** — 영웅마다 단일 / 범위(AoE) / 체인(chain) / 라인(line) / 멀티샷 / 채널링(빔·회전 베기) 등 서로 다른 공격을 가진다. 여기에 트레잇(오라·피흡·N타 강공 등)과 플레이어가 직접 발동하는 액티브 스킬이 얹힌다.
- **적** — 근접 / 원거리, 일반 / 정예 / 보스. 9종의 특성(은신·비행·저지불가·폭주·재생·타수 보호막·잠행·수영·화염)이 비트 플래그로 조합된다.
- **기믹 타일** — 물 / 불 / 모래바람 / 바람벽 등 지형 타일이 적·영웅과 상호작용한다(예: 수영 특성 적만 물을 건너고, 화염족은 불 타일에서 강해진다).
- **맵 확장** — 게임을 진행하며 잠긴 맵 모듈을 해금해 방어 구역과 배치 공간을 넓힌다.

---

## 기술 스택

| 영역 | 사용 기술 |
| --- | --- |
| DI 컨테이너 | [VContainer](https://github.com/hadashiA/VContainer) 1.18 |
| 비동기 | [UniTask](https://github.com/Cysharp/UniTask) |
| 경로 탐색 | 타일 이웃 그래프 기반 커스텀 A\* (이진 힙 open-set, 사전 계산 휴리스틱) |
| 데이터 | CSV → `DataTable` 임포트 파이프라인 + 런타임 로컬라이즈 |
| 상태 관리 | 게임 / 영웅 FSM (`Day`·`Night`·`Result`·`GameOver`) |
| 렌더링 | URP 17.3, ShaderGraph, VFX Graph |
| 최적화 | 오브젝트 풀링, 화면 밖 VFX 컬링 |
| 저장 | 암호화 세이브/로드, 결정론적 시드(seed) 기반 재현 |
| 패키지 관리 | UPM (git 의존성) + NuGetForUnity |

---

## 시스템 아키텍처

### 타일 맵 & 경로 탐색 (A\*)

`Assets/Script/Map/Core/` — `MapBoard.cs` / `Tile/` / `Pathfinder.cs` / `TileHeap.cs`

- 콜라이더가 아닌 **타일 이웃 그래프**로 이동·사거리·점유를 판정한다.
- `MapBoard.Build()` 시점에 각 타일 → 최근접 본진 칸 거리(`_coreDistance`)를 미리 계산해 A\* 휴리스틱으로 재사용하고, open-set 은 이진 힙(`TileHeap`)으로 관리해 매 스텝 선형 탐색을 제거한다.
- `CellFromRay()` 는 타일을 윗면 한 장이 아니라 바닥까지 이어진 **기둥(column slab)** 으로 보고 레이와 교차시킨다. broad-phase 바운즈 컬링 + 보드 로컬 좌표계 변환으로 **맵이 회전돼 있어도** 정확히 클릭 판정한다.
- 부수 시스템: 전장의 안개(`Map/Fog/`), 모듈 단위 맵 확장·해금(`MapAssemble.cs` + `ExpandEvent.cs`), 기믹 타일(`Core/Gimmick/`), 경로/이동 궤적 시각화(`Route`·`Trail`), 카메라 리그·클램프(`Core/Camera/`).

### 영웅 전투 & 데이터 주도 공격 시스템

`Assets/Script/Hero/`

`AttackDataSO`(`Hero/AttackDataSO/AttackDataSO.cs`) 하나로 **공격 한 종류를 통째로 저작**한다 — 탐지 범위·형태, 타격 형태(단일·범위·체인·라인), 멀티샷·타겟 분배, 타이밍(Discrete vs Continuous 채널링), 버프·디버프·장판(ground zone)·이펙트·사운드까지 모두 필드로 노출하고 `OnValidate()` 로 잘못된 조합을 에디터에서 경고한다. 현재 약 51개의 AttackData 에셋이 있다.

배달(delivery)과 실행(executor)을 두 축의 전략 패턴으로 분리했다.

```csharp
// 타이밍 축: 어떻게 "발동"되는가
public interface IAttackDeliveryStrategy { ... }
//  ├─ DiscreteAttackStrategy   : 애니메이션 이벤트 윈도우 기반 1회 집행
//  └─ ContinuousBeamStrategy   : tick 주기로 채널링 (Beam / ProjectileVolley / SelfArea)

// 전투 타입 축: 무엇이 "때리는가" — {SwordMan,Archer,Mage,Healer}AttackState 가 주입
public interface IAttackExecutor { ... }
//  ├─ MeleeAttackExecutor
//  ├─ RangedAttackExecutor     : 투사체 발사 (근접 영웅도 데이터로 원거리화 가능)
//  └─ HealAttackExecutor       : 아군 대상
```

- `ChainResolver` — 체인 공격 튕김 대상 선정 + falloff, `BeamLinkEffect` 로 두 지점 연결 이펙트를 추적.
- `AttackTargetSelector` — 멀티샷 대상 분배(타일 순회 순서 안정성 보장).
- `AttackDamageUtil` — 데미지·크리·장판·디버프 적용을 한곳에 모은 계산 유틸.
- `AttackAnimSpeedUtil` — 공격 속도 스탯에 맞춰 애니메이션 클립 배속을 정규화.
- **트레잇** (`Hero/HeroTrait/`, 10종) — `OnAttackPerformed` / `OnHit` / `OnKill` / `OnDayStart` 등 훅에 반응하는 컴포넌트(오라, 피흡, N타 강공, 확률 재발동, 스택형 최대치, 스탠스 전환 등).
- **액티브 스킬** — `HeroActiveSkill` + `HeroSkillCastController`. 클릭 → 타일 타겟 → 채널링/즉발 분기, `CancellationToken` 으로 사망·디스폰 시 안전하게 취소.
- **합성 / 업그레이드** — `HeroCombineManager`(드래그 또는 맵 위 더블클릭으로 합성, 티어 1→4), `HeroStatManager`(티어·클래스·스탯 업그레이드 결과를 `HeroData` 단위로 캐싱하고, 진행 중인 버프 Modifier 를 유지한 채 base 스탯만 갱신).
- 영웅 FSM 상태(`Hero/HeroState/` — Idle·Attack·Death·Stun·Skill), 타일 기반 사거리·타겟팅(`Map/Core/Static/TileShapeQuery.cs`).

### 적 & 웨이브

`Assets/Script/Enemy/` — `WaveSpawner.cs` / `EnemyBase.cs`

- `MapBoard.GetWaypoints()` 로 계산된 스폰 → 본진 경로를 따라 이동하며 `PoolManager` 로 풀링된다.
- 특성 9종은 `[Flags]` 열거형(`EnemyEnum.cs`)으로 조합되어 저지·피격·이동 규칙과 기믹 타일 상호작용을 바꾼다.
- 디버프 시스템(`Assets/Script/Debuff/`) — DoT 드라이버, 디버프 트래커, 상태 디버프 ScriptableObject. 영웅 공격과 적 스킬이 같은 시스템을 공유한다.

### 낮/밤 사이클 & 게임 상태

- `GameManager.cs` — FSM(`DayState` / `NightState` / `ResultState` / `GameOverState`), `CanBuild` / `CanSpawnEnemy` 게이팅, 본진 HP, 날짜 카운트.
- `EnviromentManager.cs` — 낮/밤 전환 시 태양광 색·강도, 앰비언트, 스카이박스를 `UniTask` 로 부드럽게 보간하고 BGM 을 페이드로 교체(낮/밤 각 3트랙 랜덤).

### 경제 & 건설

`Assets/GameLoop(ChoiJinWoo)/Scripts/`

- `ResourcesManager` + `FacilityManager` + `ProductionEconomyConfig`(SO) 로 자원 생산·소비를 관리.
- `Infrastructure/` — 지역별 시설 슬롯(`RegionFacilitySlots`), 건설 흐름(`BaseConstructor`, `BuildableFacility`).
- 시민 시스템(`Citizen/`) — 집, 시민 배회, 밤 귀가.

### 저장 / 불러오기

`Assets/Script/SaveLoad/`

- `SaveManager` 는 `GameManager` 의 낮/밤 전환 이벤트만 구독해 페이즈별(`DayStart` / `NightReady` / `DayActive`)로 자동 저장한다. 저장이 실패해도 게임 진행은 막지 않고 직전 정상 저장본을 유지한다.
- `SaveCipher` + `SaveKey` 로 암호화, `SaveCheck`(파일 태그 + 버전 검증), 다중 슬롯.
- 복원은 `LoadManager` 가 전담. 게임 시드(`GameManager.GameSeed`)로 영웅 뽑기·합성 순번까지 재현되어, 같은 저장본을 불러오면 항상 같은 결과가 나온다.

### 의존성 주입 (VContainer)

`Assets/GameLoop(ChoiJinWoo)/Scripts/VContainer/GameLifeTimeScope.cs`

- 매니저·세이브 계층·풀 매니저를 컨테이너에 등록한다. 아무도 생성자 의존성으로 요구하지 않아 lazy 로는 만들어지지 않는 싱글턴은 `RegisterBuildCallback` 에서 강제 `Resolve` 해 초기화 순서를 보장한다.

### 데이터 파이프라인 (CSV)

`Assets/Script/DataTable/`

- 영웅 / 영웅 스탯 / 스킬 / 웨이브 / 포탈 테이블을 CSV 로 관리하고 `DataTable` 로 임포트한다. 밸런싱을 코드 수정 없이 반복할 수 있다.
- `LocalizeTextManager` — 런타임 다국어 텍스트.

### VFX & 성능 최적화

- `Assets/Script/Common/VfxVisibility.cs` — 순수 연출용 이펙트는 **화면 밖(마진 포함)이면 인스턴스화 자체를 생략**하고, 계속 살아있어야 하는 이펙트는 매 프레임 렌더러/파티클 가시성만 토글한다.
- 이펙트·투사체 풀을 **영웅 인스턴스가 소유**한다 — 씬이 언로드되어 영웅이 파괴되면 풀도 함께 사라져 dangling 참조가 없다.
- `GroundZoneEffect`(장판) — 프리팹을 풀에서 꺼내 위치만 잡아주면 데미지/힐 tick·소멸·풀 반납을 컴포넌트가 스스로 처리한다.
- `BeamLinkEffect` — 빔/체인 이펙트가 움직이는 대상을 매 프레임 따라가도록 끝점을 추적한다.

### 튜토리얼

`Assets/GameLoop(ChoiJinWoo)/Scripts/Tutorial/`

- 0일차 연습 흐름, 단계 정의(SO), 입력 게이트, 오버레이 UI. 0일차의 임시 상태는 저장하지 않는다.

---

## 에디터 툴링

- **CSV 임포터** — `HeroTableImporter` / `HeroStatTableImporter` / `SkillTableImporter` 로 CSV → ScriptableObject 일괄 생성·갱신.
- **커스텀 인스펙터** — `AttackDataSOEditor` 등.
- **시뮬레이션 창** — `HeroSimWindow` / `EnemySimWindow` 로 스탯·코스트를 계산해 밸런스를 검토.
- **치트 뷰** — `DayFlowCheatView` / `HeroUpgradeCheatView` / `BaseStateCheatView` / `ResourceGrantView` 로 낮·밤 흐름, 업그레이드, 자원을 즉시 조작해 테스트.
- **마이그레이션** — 데이터 스키마 변경 시 기존 에셋을 일괄 변환(`AttackDataMigration`, `HeroEffectLifetimeMigration`).
- **타일 저작 툴** — `Assets/Script/Map/Editor/`.
- **세이브 툴** — `SaveDumpWindow`(저장 내용 덤프), 반복 저장 회귀 테스트.

---

## 프로젝트 구조

```
Assets/
├── Script/
│   ├── Hero/            영웅, 데이터 주도 공격 시스템, 트레잇, 스킬, 합성·업그레이드
│   ├── Map/             타일 보드, A* 길찾기, 기믹 타일, 안개, 맵 확장, 카메라
│   ├── Enemy/           적 AI, 웨이브 스포너, 특성, 도감, 사운드
│   ├── Debuff/          DoT·상태 디버프 (영웅/적 공용)
│   ├── DataTable/       CSV 데이터 테이블 + 로컬라이즈
│   ├── SaveLoad/        암호화 세이브/로드, 슬롯, 자동 저장
│   └── Editor/          임포터, 시뮬레이션 창, 치트 뷰, 마이그레이션
├── GameLoop(ChoiJinWoo)/Scripts/
│   ├── Util/            GameManager (FSM), 게임 상태
│   ├── Enviroment/      낮/밤 전환 (조명·스카이박스·BGM)
│   ├── ProductionFacility/, Infrastructure/   경제·건설
│   ├── Citizen/         시민 시스템
│   ├── Tutorial/        튜토리얼
│   ├── UI/              HUD, 스킬 패널, 빌드 패널 등
│   └── VContainer/      GameLifeTimeScope (DI)
├── HeroData/            영웅·스탯·공격 ScriptableObject
├── Resources/DataTable/ 런타임 로드용 CSV
└── MainScene/           Title.unity, MainScene.unity
```

---

## 빌드 / 실행

1. **Unity `6000.3.15f1`** 로 프로젝트를 연다 (Unity 6.3).
2. `Packages/manifest.json` 의 git 의존성(UniTask, VContainer, NuGetForUnity, MCP for Unity)은 최초 오픈 시 자동으로 복원된다.
3. 진입 씬: `Assets/MainScene/Title.unity`
4. 대상 플랫폼: Android / iOS (에디터 플레이 모드로도 전체 게임 루프를 테스트할 수 있다).
