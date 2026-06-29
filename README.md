# 🤖 문명도굴단 (JunkRaiders)

> 제한 시간 안에 폐품을 모아 목표 금액을 달성하는 **네트워크 기반 협동 멀티플레이 게임**

<br>

## 📌 프로젝트 소개

폐허가 된 도시에서 팀원들과 협력해 폐품을 수거하고 정산소에 팔아 목표 금액을 달성하세요.  
시간, 배터리, 적, 운반 리스크를 관리하며 탈출 포털을 활성화하는 것이 목표입니다.

- **장르**: 3D 협동 멀티플레이 액션/수집
- **플랫폼**: PC (Windows)
- **개발 기간**: 2025.05 ~ 2026.06 (약 1~2개월)
- **개발 인원**: 7인 팀 프로젝트

<br>

## 🛠 기술 스택

| 분류 | 사용 기술 |
|------|-----------|
| 엔진 | Unity 3D |
| 네트워크 | Photon Fusion 2 |
| 언어 | C# |
| 버전 관리 | Git / GitHub |
| 협업 | Notion, Discord |

<br>

## 🎮 주요 기능

### 🔗 네트워크 동기화
- `FusionRoomManager` 기반 방 생성/참가 및 씬 전환
- `[Networked]` 프로퍼티로 CurrentMoney, RemainingTime, IsGameEnded 전체 클라이언트 동기화
- 중요 행동(정산/장착/탈출)은 `RPC(All → StateAuthority)` 패턴으로 Host에서 검증 후 처리

### 🦾 모듈형 팔 장비
- `IArmsBase` 인터페이스 기반 확장 구조
- CarryArm / LiftArm / LightArm / RailgunArm 구현
- 새 팔 장비 추가 시 인터페이스만 구현하면 `ArmsManager`가 자동으로 처리

### 🔋 배터리 시스템
- 이동 / 달리기 / 팔 사용에 따른 3중 소모 구조
- 7단계 머티리얼 게이지로 시각 피드백
- 배터리 0 도달 시 행동 제한 및 사망 판정 (`OnBatteryEmpty` 이벤트)

### 💰 폐품 회수 & 정산
- `Carryable` 기반 네트워크 운반 동기화
- 피격 시 폐품 가치 실시간 감소 (`SalvageDamageHandler`)
- 정산소 도착 → Host에서 가격 합산 → `NetworkGameManager.AddMoney()` → 전체 HUD 반영

### 🤖 적 AI
- Fusion Addons FSM 기반 이중 상태 머신
  - `_enemyBasicAI`: Idle → Attack → Dead → StatusEffect
  - `_enemyMovementAI`: Patrol → Chase
- `VisionDetector`가 5tick마다 시야 체크 → 대상 발견 시 Chase 전환
- NavMeshAgent 기반 경로 탐색

<br>

## 🔥 트러블슈팅

### 탈출 성공 시 클라이언트 결과 영상 오판정
**문제**: 호스트에서 `IsGameEnded`와 `HasEscaped`를 같은 tick에 변경했을 때, 클라이언트에서 `IsGameEnded`가 먼저 도착해 `HasEscaped`가 아직 `false`인 상태로 결과를 판정해 성공 플레이어에게 실패 영상이 재생되는 문제

**원인**: Fusion 2의 `[Networked]` 값은 snapshot 단위로 전파되며, 같은 tick에 변경된 두 값이 클라이언트에 동시에 도착함을 보장하지 않음

**해결**: `HandleGameEnded()`에서 즉시 판정하지 않고, `ShowResultWhenResolved()` 코루틴으로 `HasEscaped` 또는 `IsDead` 중 하나가 확정될 때까지 대기(최대 2초 timeout) 후 결과 화면 표시

```csharp
private IEnumerator ShowResultWhenResolved()
{
    float timeout = Time.realtimeSinceStartup + 2f;
    while (!localHandler.IsDead && !localHandler.HasEscaped
           && Time.realtimeSinceStartup < timeout)
    {
        yield return null;
    }
    ShowResult(localHandler.HasEscaped && !localHandler.IsDead);
}
```

### 정산 구역 내 폐품 허공 고정 버그
**문제**: 운반 중인 폐품이 정산 구역 트리거에 닿는 순간 `Carryable`이 비활성화되어 추적 로직이 멈추고 허공에 고정되는 문제

**원인**: `OnTriggerEnter` 시점에 즉시 `Carryable.enabled = false` 처리

**해결**: `Update()`에서 `IsCarried == false` 확인 후 잠금 처리로 변경해 드롭 이후에만 집기 차단

<br>

## 👥 팀원 소개

| 이름 | 역할 | GitHub |
|------|------|--------|
| YEONJUN LEE | 네트워크(NET) | [@typhoon36](https://github.com/typhoon36) |
