# Wraith Bound — 게임 분석 문서

> 작성일: 2026-06-01  
> 프로젝트 경로: `Wraith Bound/`  
> Unity 버전: **6000.3.10f1** (Unity 6)

---

## 목차

1. [개요](#1-개요)
2. [프로젝트 구조](#2-프로젝트-구조)
3. [플레이어 시스템](#3-플레이어-시스템)
4. [적 / AI 시스템](#4-적--ai-시스템)
5. [맵 / 절차적 생성](#5-맵--절차적-생성)
6. [아이템 / 장비 시스템](#6-아이템--장비-시스템)
7. [UI 시스템](#7-ui-시스템)
8. [데이터 아키텍처 (ScriptableObject)](#8-데이터-아키텍처-scriptableobject)
9. [주요 메커닉](#9-주요-메커닉)
10. [개발 현황 (완성 vs WIP)](#10-개발-현황-완성-vs-wip)
11. [기술 스택 및 패키지](#11-기술-스택-및-패키지)
12. [핵심 스크립트 맵](#12-핵심-스크립트-맵)

---

## 1. 개요

| 항목 | 내용 |
|------|------|
| **장르** | 1인칭(FPS) 공포 · 생존 · 스텔스 |
| **배경** | 병원 (`Assets/ExternalAssets/The_Horror_Hospital`) |
| **핵심 루프** | 허브 → 심연 추락 → 챕터1 입장 → 타일 기반 맵 탐색 → 적 회피·숨기 → 아이템 수집·장비 활용 |
| **적 유형** | Ghost(문 통과), Monster(문 파괴), The Mimic(문 돌진) |
| **빌드 씬** | Hub → Chapter1 (2씬만 등록) |

**Wraith Bound**는 귀신(wraith)과 플레이어의 구속·탈출 구조를 암시하는 1인칭 공포 생존 프로토타입입니다. 허브에서 심연으로 떨어져 Chapter1의 타일 조립 맵에서 플레이하며, 숨기·인벤토리·장비(스마트폰 레이더)·적 AI까지 핵심 골격이 갖춰져 있습니다.

---

## 2. 프로젝트 구조

### 2.1 Assets 폴더

| 폴더 | 역할 |
|------|------|
| `Assets/Scripts/` | 전 게임플레이 C# (약 56개, TMP 예제 제외) |
| `Assets/Scenes/` | 플레이·테스트 씬 |
| `Assets/Prefab/` | 플레이어, 맵 타일, 오브젝트, 아이템 |
| `Assets/Resources/ItemDatas/` | 아이템 ScriptableObject 데이터 |
| `Assets/Model/Enemy/` | 적 모델·몬스터 SO |
| `Assets/ExternalAssets/` | 병원·서바이벌 아이템 에셋 팩 |
| `Assets/Settings/`, `URPDefaultResources/` | URP 렌더 설정 |

### 2.2 씬 목록

| 씬 | 경로 | 용도 |
|----|------|------|
| **Hub** | `Assets/Scenes/Chapter/Hub.unity` | 빌드 0번 — `AbyssSinker` 허브 |
| **Chapter1** | `Assets/Scenes/Chapter/Chapter1.unity` | 빌드 1번 — `MapGenerator` + `SceneStarter` |
| PlayerScene | `Assets/Scenes/PlayerTestScene/PlayerScene.unity` | 플레이어 테스트 |
| ItemScene | `Assets/Scenes/ItemScene.unity` | 아이템·Ghost 태그 테스트 |
| EnemyScene | `Assets/Scenes/EnemyScene/EnemyScene.unity` | Monster / The Mimic |
| SoundCheckScene | `Assets/Scenes/EnemyScene/SoundCheckScene.unity` | 사운드 체크 |

### 2.3 맵 타일 프리팹 (Chapter1)

`Assets/Prefab/Map/Tile/Chapter1/` — Lobby, Ward, Corridor, OperatingRoom, Stair 1~3, Storage 등 + `Protocol/` 베이스 타일

---

## 3. 플레이어 시스템

### 3.1 이동 — `PlayerController.cs`

- **CharacterController** 기반: 걷기 / 달리기(Shift, 스태미나 소모) / 웅크리기(Left Control)
- 지면 SphereCast, 천장 CheckCapsule, 중력·낙하 처리
- **낙하 데미지:** 20m 이상 + 속도 조건 시 `PlayerConditions.onDamage` 호출
- 입력: 레거시 `Input.GetAxisRaw` / `GetKey` (Input System 패키지는 설치되어 있으나 이 스크립트는 미연동)

### 3.2 시점 — `MouseLook.cs`

- 1인칭 마우스 룩 (숨기 중에는 비활성화)

### 3.3 상호작용 — `PlayerInteractor.cs`

- 카메라 정면 Raycast → `IInteractable` + `"Interact"` 입력
- `InteractionCrosshairUI`에 아이템명 표시

### 3.4 인벤토리 — `InventoryManager.cs`, `InventorySlot.cs`

- 기본 3칸, 최대 7칸 (패시브 아이템으로 `extraSlots` 확장)
- 타입 분류:
  - **Active / Equip** → 일반 슬롯
  - **Passive** → 전용 `passiveSlot`
- 가득 찬 경우 선택 슬롯과 교체, 바닥 드롭은 `WorldItemSpawnHelper`
- 휠·숫자키 1~9 슬롯 선택, `holdPos` / `EquipmentViewController`로 손·뷰 표시

### 3.5 컨디션 — `PlayerConditions.cs`

- HP, 스태미나, `onDamage`, `RecoverHealth` / `RecoverStamina`, `Die()` (현재 로그만)
- **알려진 버그:** `onDamage`에서 데미지가 **두 번** 적용됨

```csharp
currentHealth -= damage;
currentHealth = Mathf.Max(currentHealth - damage, 0); // 중복 차감
```

### 3.6 숨기 — `PlayerHidingController.cs`, `HidingSpot.cs`

- `HidingSpot` 정면 각도·거리 검사 후 E(Interact)로 진입
- 진입 시 **CharacterController 비활성** → 적 AI는 `!cc.enabled`로 "숨음" 판정
- `JustEnteredHiding` 플래그: **눈앞에서 숨으면 추격 지속**
- 제한된 헤드룩만 가능

### 3.7 기타

| 스크립트 | 역할 |
|----------|------|
| `RearviewCamera.cs` | 후방 카메라 |
| `SelectedItemUseController.cs` | 좌클릭 장비, 우클릭 홀드(0.75초)로 Active 사용 |

---

## 4. 적 / AI 시스템

### 4.1 공통 베이스 — `EnemyBase.cs`

**FSM 상태:** Patrol → Chase → Investigate

| 기능 | 설명 |
|------|------|
| 시야 | Raycast + `Monsters.detectRange`, Player 태그 |
| 추격 | 마지막 위치 + 이동 방향 예측 (`NavMesh.SamplePosition`) |
| 문 처리 | Chase 중 `Door` 태그 Overlap → `HandleDoor` (가상 메서드) |
| 숨기 연동 | `IsPlayerHidden` — CharacterController 비활성 시 숨음 판정 |

> **주의:** 매 프레임 `Debug.Log(currentState)` — 디버그 잔재

### 4.2 Ghost — `GhostEnemy.cs`

- Chase 시 `DoorController.OpenPath()` (NavMeshObstacle 끔)
- Chase 종료 시 `ClosePath()` — 문을 통과하는 유령

### 4.3 Monster — `MonsterEnemy.cs`

- 문 앞 공격 애니메이션 후 `door.TakeDamage(999)` → 문 파괴

### 4.4 The Mimic — `TheMimicController.cs`

- `EnemyBase`와 **별도** NavMesh 끄고 전진 돌진
- 앞 문 Raycast → Rush → 문 파괴
- 데이터: `Assets/Model/Enemy/TheMimic/Data/TheMimic.asset`

### 4.5 기타

| 컴포넌트 | 역할 |
|----------|------|
| `DoorController.cs` | NavMeshObstacle, 파괴 시 `brokenDoor` Rigidbody 활성화 |
| `Obstacle.cs` | 트리거 체류 시 0.5초마다 `ObstacleData.damage` |
| `EnemyZone.cs` | Lobby / Floor1 / Floor2 태그 (연동 제한적) |
| `Monsters.cs` (SO) | hp, moveSpeed, detectRange, viewAngle, hearingRange 등 정의 |

> **미사용 필드:** `EnemyBase`는 `detectRange`·Raycast만 사용. `viewAngle`, `hearingRange`, `obstacleLayer`는 SO에 정의만 되어 있음.

### 4.6 씬 배치 현황

- **Chapter1:** GhostEnemy 미배치 (메인 플레이에 적 없을 가능성)
- **EnemyScene:** MonsterEnemy, TheMimicController
- **ItemScene:** `Ghost` 태그 오브젝트 (레이더 테스트용)

---

## 5. 맵 / 절차적 생성

### 5.1 `MapGenerator.cs`

- **5×5 그리드**, `tileSize = 10`
- **규칙 기반** 연결:
  - 모서리 → 2방향
  - 가장자리 → 3방향
  - 내부 → 4방향
- `(2,0)`, `(2,4)`만 4방향 "허브/출구" 타일로 **강제 덮어쓰기**
- `TileSet` (`Chapter1 Tiles.asset`)에서 `Tile.openings` 비트 매칭 + 90° 회전 후 Instantiate

```csharp
[System.Flags]
public enum Dir
{
    None = 0, Up = 1, Down = 2, Left = 4, Right = 8, Everything = 15
}
```

> **한계:** 랜덤 시드·방 개수·목표 지점 경로 검증 없음. "절차적"이라기보다 **고정 규칙 그리드 + 타일 변형 선택**에 가깝습니다.

### 5.2 챕터 입장 연출 — `SceneStarter.cs`

- Chapter1 시작 시 플레이어 상공 낙하 → 페이드 → 착지 카메라 연출 → 조작 복구

### 5.3 허브 → 심연 — `AbyssSinker.cs`

- 트리거 → 조작 정지 → 1초 정지 → 아래로 이동 → `SceneManager.LoadScene("Chapter1")`

### 5.4 랜덤 아이템 — `RandomItemSpawner.cs`

- 가구/캐비넷 등에 부착, 확률·다중 스폰 포인트

---

## 6. 아이템 / 장비 시스템

### 6.1 데이터 계층

| 클래스 | 파일 | 역할 |
|--------|------|------|
| `Items` | `Items.cs` | 추상 SO: id, type, icon, maxCount, breakage, Use() |
| `ActiveItem` | `ActiveItem.cs` | value, deployPrefab, 설치형 |
| `Equipment` | `Equipment.cs` | useMode, maxEnergy, range |
| `PassiveItem` | `PassiveItem.cs` | statModifier, extraSlots |

### 6.2 등록된 아이템 (`Assets/Resources/ItemDatas/`)

**Active (소모품)**
- EnergyDrink, EnergyBar, Battery, Key, MRE, Jerky, Talisman, RandomBox

**Equipment (장비)**
- SmartPhone, FlashLight, Compass, Camera, HandGun

**Passive (패시브)**
- PassiveBag_ExtraSlots2

### 6.3 사용 흐름

```
월드 픽업 (ItemObject)
  → TryAcquireItem (InventoryManager)
  → ItemEffectManager.Use (획득 시)
  → Destroy

선택 슬롯 사용 (SelectedItemUseController)
  → Equip: ToggleOnClick (손전등 Light), SmartPhone → SmartPhoneHolderToggle
  → Active: 우클릭 0.75초 홀드 후 ActiveItem.Use
```

### 6.4 스마트폰 장비

| 스크립트 | 역할 |
|----------|------|
| `RadarSystem.cs` | Ghost 태그 탐지, UI 도트, 스캔라인 각도 |
| `BatteryManager.cs` | 배터리 관리 |
| `CompassLogic.cs` | 나침반 |
| `SonarRotation.cs` | 소나 회전 |
| `EquipmentViewController.cs` | 전용 카메라·PickupItem 레이어 뷰 |

### 6.5 미완성 부분

`ItemEffectManager.cs` — 소모품 회복은 **Debug.Log 수준**이며 `RecoverHealth`/`RecoverStamina` 호출 없음:

```csharp
void ApplyActiveEffect(ActiveItem active)
{
    Debug.Log($"{active.itemName} 사용! {active.value}만큼 효과 발생.");
    // 예: player.Heal(active.value);  ← 미구현
}
```

---

## 7. UI 시스템

| 스크립트 | 상태 | 역할 |
|----------|------|------|
| `InventoryUI.cs` | ✅ 동작 | Tab 인벤 토글, 퀵슬롯 아이콘/이름 |
| `InteractionCrosshairUI.cs` | ✅ 동작 | 런타임 생성 크로스헤어 + Pol 폰트 |
| `PlayerConditionUI.cs` | ❌ 비활성 | **전체 주석 처리** — HP/스태미나 UI 없음 |
| `SmartPhoneHolderToggle.cs` | ✅ 동작 | 스마트폰 홀더 토글 |
| `UIFontConfig` | ✅ | `Assets/Resources/UIFontConfig.asset` |
| `DoorClick.cs` | 레거시 | **OnGUI** 메시지 (E키, 마우스 Ray) |

---

## 8. 데이터 아키텍처 (ScriptableObject)

```
Items (abstract SO)
├── ActiveItem
├── Equipment (+ EquipmentUseMode enum)
└── PassiveItem

Monsters SO          → EnemyBase, TheMimicController
ObstacleData SO      → Obstacle
TileSet SO           → MapGenerator (GameObject[] tiles)
UIFontConfig SO      → InteractionCrosshairUI
```

- **CreateAssetMenu:** `Custom/Items/*`, `Custom/Monsters`, `Custom/ObstacleData`
- **인터페이스:** `IInteractable` — `InteractableItem.cs`, `ItemObject.cs`
- **적 데이터 예:** `Assets/Model/Enemy/MonsterMutant 7/Data/LobbyMon1.asset`, `TheMimic.asset`

---

## 9. 주요 메커닉

| 메커닉 | 구현 파일 | 비고 |
|--------|-----------|------|
| 문 (플레이어) | `DoorClick.cs` | 스윙/슬라이딩, OnGUI |
| 문 (AI) | `DoorController.cs` | NavMesh + 파괴 |
| 잠금 문 | `LockDoor.cs` + `Lever.cs` | 레버 E키 → Unlock (Interact 미통일) |
| 옷장 문 | `WardrobeDoorInteract.cs` | 이름에 "Door" 포함 시 회전 |
| 로프 | `RopeScript.cs` | Rigidbody 밀기 |
| 허브/심연 | `AbyssSinker.cs` | Hub → Chapter1 |
| 챕터 시작 낙하 | `SceneStarter.cs` | Chapter1 전용 |
| 숨기 | `PlayerHidingController` + `HidingSpot` | Prefab: Cabinet, Wardrobe |
| 테스트 파괴 | `DoorBrokenTest.cs` | 개발용 |

---

## 10. 개발 현황 (완성 vs WIP)

### ✅ 비교적 완성된 부분

- 1인칭 이동·스태미나·웅크리기·낙하 데미지
- 인벤토리 구조 (타입 분리, 교체, 패시브 슬롯)
- 숨기 + 적 AI 연동 (추격/조사/눈앞 숨기)
- Ghost/Monster 문 상호작용, The Mimic 돌진
- 5×5 타일 맵 빌드 + Chapter1 씬 통합
- Hub → Chapter1 플로우, 입장·심연 연출
- 스마트폰 레이더, 손전등 토글, 장비 뷰 카메라
- 랜덤 아이템 스포너, 월드 아이템 프리팹

### ⚠️ WIP / 미완 / 기술 부채

| 항목 | 설명 |
|------|------|
| 소모품 실제 효과 | `ItemEffectManager` → HP/ST 회복 미연결 |
| 체력 UI | `PlayerConditionUI` 전면 비활성 |
| 사망 처리 | `Die()` 로그만, 게임오버 UI·씬 전환 없음 |
| 데미지 중복 | `PlayerConditions.onDamage` 이중 차감 버그 |
| Monsters SO | viewAngle, hearingRange 등 미사용 |
| Chapter1 적 배치 | Ghost 미배치, EnemyScene·ItemScene 위주 |
| 입력 시스템 | Input System vs Legacy Input 혼재 |
| 문 시스템 | DoorClick / DoorController / Wardrobe 3종 통합 필요 |
| 맵 생성 | 진정한 절차 생성 아님, 크기 고정 5×5 |
| 디버그 잔재 | `EnemyBase` 매 프레임 Debug.Log |
| 상호작용 API | `Lever` — KeyCode.E 하드코딩, `PlayerInteractor`와 불일치 |
| 문서 | README·게임 디자인 문서 없음 |

---

## 11. 기술 스택 및 패키지

| 항목 | 버전 |
|------|------|
| Unity Editor | **6000.3.10f1** |
| Render Pipeline | **URP 17.3.0** |
| Input System | **1.18.0** (패키지 설치됨, gameplay는 대부분 Legacy) |
| AI Navigation | **2.0.12** |
| uGUI | **2.0.0** |
| Timeline | 1.8.10 |
| Visual Scripting | 1.9.9 |
| Collab Proxy | 2.11.3 |
| unity-mcp | CoplayDev (개발용 MCP 연동) |

---

## 12. 핵심 스크립트 맵

```
Assets/Scripts/
├── Player/
│   ├── PlayerController.cs       # 이동·스태미나·낙하
│   ├── PlayerConditions.cs       # HP/ST·데미지·사망
│   ├── PlayerInteractor.cs       # Raycast 상호작용
│   ├── PlayerHidingController.cs # 숨기
│   ├── InventoryManager.cs       # 인벙토리
│   ├── InventorySlot.cs
│   ├── MouseLook.cs
│   └── RearviewCamera.cs
│
├── Enemy/
│   ├── EnemyBase.cs              # FSM AI 베이스
│   ├── Ghost/GhostEnemy.cs       # 문 통과 유령
│   ├── Monster/MonsterEnemy.cs   # 문 파괴 괴물
│   ├── Monster/TheMimic/TheMimicController.cs
│   ├── Obstacle/Obstacle.cs
│   └── EnemyZone.cs
│
├── Map/
│   ├── Script/MapGenerator.cs    # 5×5 타일 생성
│   ├── Script/Tile.cs, TileSet.cs
│   ├── Map/SceneStarter.cs       # 낙하 연출
│   ├── Hub/AbyssSinker.cs        # 허브→챕터 전환
│   └── Object/All/
│       ├── DoorController.cs, DoorClick.cs, LockDoor.cs
│       ├── Lever.cs, RopeScript.cs
│       ├── WardrobeDoorInteract.cs
│       └── RandomItemSpawner.cs
│
├── Item/
│   ├── ItemObject.cs
│   ├── ItemEffectManager.cs
│   ├── SelectedItemUseController.cs
│   ├── EquipmentViewController.cs
│   └── Equipment/Smartphone/     # RadarSystem, BatteryManager 등
│
├── UI/
│   ├── InventoryUI.cs
│   ├── InteractionCrosshairUI.cs
│   └── PlayerConditionUI.cs      # (비활성)
│
└── ScriptableObject/
    ├── Item/Items.cs, ActiveItem.cs, Equipment.cs, PassiveItem.cs
    ├── Monsters.cs
    └── ObstacleData.cs
```

---

## 요약

**Wraith Bound**는 Unity 6 + URP 기반 병원 배경 1인칭 공포 생존 프로토타입입니다. 허브→심연→Chapter1 흐름, 타일 맵 생성, 숨기·인벤토리·장비·적 AI까지 **핵심 골격은 갖춰져** 있습니다.

다음 우선 작업으로 보이는 항목:
1. `PlayerConditions.onDamage` 이중 차감 버그 수정
2. `ItemEffectManager` → HP/ST 회복 연결
3. `PlayerConditionUI` 활성화
4. Chapter1 적 배치 및 `Monsters` SO 필드 활용
5. 문/상호작용 API 통합 (`PlayerInteractor` 기준)
