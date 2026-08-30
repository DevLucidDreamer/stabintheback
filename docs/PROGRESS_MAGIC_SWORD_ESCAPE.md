# 마검 탈출 성소 개발 진행 기록

작성일: 2026-08-30  
대상 씬: `Assets/Scenes/Stage4_MagicSwordEscape.unity`

## 1. 조사와 설계 기준

프로젝트의 `기획서.md`, `기획서_저주받은_성채.md`, `개발_로드맵.md`와 Stage3 및 전투·사망·무기 네트워크 구현을 함께 점검했다.

기존 Stage3는 압력판 → 분산 룬 → 쌍레버 → 무기 봉인 → 전원 집결의 협동 골격이 이미 안정적으로 잡혀 있었다. 반면 마검 탈출 장르의 핵심인 탐색, 숨겨진 장치, 제한된 마검을 둘러싼 권력 이동, 협동 중 배신 가능성이 약했다. 회귀 위험을 줄이기 위해 Stage3를 덮어쓰지 않고 별도 Stage4 프로토타입으로 확장했다.

## 2. 구현된 게임 흐름

1. 숨은 봉인 스위치 3개를 미로와 막다른 길에서 탐색한다.
2. 서로 떨어진 동·서쪽 기록을 음성으로 합쳐 `달 → 불 → 가시 → 눈` 순서로 룬을 누른다.
3. 짧고 넓은 무너진 다리를 건너 양쪽 압력판을 동시에 유지한다.
4. 갈라진 길 끝의 쌍레버를 7초 안에 서로 다른 플레이어가 당긴다.
5. 숨은 보관실의 마검 2개만 차지할 수 있으며, 마검으로 봉인핵 3개를 파괴한다.
6. 원킬·사망 시 마검 드롭 규칙을 유지한 채 모든 생존 플레이어가 출구에 2초 동안 집결한다.
7. 성공 배너를 표시하고 8초 뒤 Lobby로 복귀한다.

1인 테스트에서는 압력판과 쌍레버를 순차 입력할 수 있는 보조 규칙이 적용된다. 2인 이상에서는 압력판과 레버에 서로 다른 플레이어가 필요하다.

## 3. 네트워크와 복구 규칙

- 진행 상태는 `MagicEscapeGameManager`의 Mirror `SyncVar`로 서버가 관리한다.
- 상호작용은 거리·영역·쿨다운·무기 보유 여부를 서버에서 다시 검증한다.
- 잘못 누른 룬은 순서만 초기화하며 스테이지 전체를 초기화하지 않는다.
- 쌍레버 제한시간이 끝나면 레버 상태만 초기화한다.
- 발판과 집결 구역은 로컬 플레이어의 `CharacterController.bounds`와 영역을 겹침 검사한다.
- 접속 종료 플레이어는 발판·집결 명단에서 제거한다.
- 중도 참가자는 현재 단계와 영구 진행 상태를 동기화 받고, 열린 문을 통과해 합류할 수 있다.
- 기존 `WeaponNetworkManager`와 `PlayerHealth`를 재사용해 원킬, 즉시 리스폰, 사망 위치 무기 드롭을 유지한다.

## 4. 생성·연결된 콘텐츠

- 신규 씬: `Stage4_MagicSwordEscape`
- 신규 자동 생성기: `Stage4MagicEscapeBuilder`
- 신규 런타임 구성요소:
  - `MagicEscapeGameManager`
  - `MagicEscapeSwitch`
  - `MagicEscapeRune`
  - `MagicEscapePressurePlate`
  - `MagicEscapeLever`
  - `MagicEscapeSeal`
  - `MagicEscapeRallyZone`
  - `MagicEscapeGate`
  - `MagicEscapeHud`
- 신규 전용 머티리얼 11개
- 로비 스테이지 선택 항목: `마검 탈출 성소`
- Build Settings 순서: MainTitle → Lobby → Stage3 → Stage4 → Stage2
- 전체 콘텐츠 재생성 도구에도 Stage4 생성 단계를 연결했다.
- 기존 Stage3의 집결 영역도 발판과 같은 견고한 충돌 판정으로 보강했다.

## 5. 자동 검증 결과

- Unity 6.3.15f1 C# 컴파일: 통과
- Stage4 자동 생성 및 직렬화: 통과
- NetworkIdentity sceneId: 진행 관리자와 무기 관리자 모두 생성 확인
- 씬·프리팹 Missing Script 및 끊어진 참조 검사: 통과
- Windows x64 전체 빌드: 성공 (`Build Finished, Result: Success`)
- Windows 무그래픽 기동 스모크 테스트: 예외 0건
- `git diff --check`: 공백 오류 없음

### 배포 전 남은 외부 서비스 확인

빌드 로그에는 Unity Services가 현재 에디터 세션을 Cloud 프로젝트와 연결되지 않은 것으로 판단하는 경고가 1건 남아 있다. `ProjectSettings.asset`에는 Project ID가 저장되어 있지만, 이 상태에서는 Relay와 Vivox의 실제 서비스 인증까지 통과했다고 볼 수 없다.

Unity Hub에 로그인한 상태로 프로젝트를 열고 `Edit > Project Settings > Services`에서 기존 Cloud 프로젝트를 다시 연결한 다음, Dashboard에서 Relay와 Vivox가 같은 Project ID에 활성화되어 있는지 확인한다. 이후 출시 빌드를 다시 실행해야 한다. `ReleaseBuildValidator`도 앞으로 이 활성 연결 상태를 실패 조건으로 검사하도록 보강했다.

- Unity 공식 프로젝트 연결 절차: <https://docs.unity.com/en-us/cloud/projects/configure-project-for-unity-cloud>
- Unity 공식 Vivox 연결 절차: <https://docs.unity.com/en-us/vivox-unity/vivox-unity-first-steps>

배포 파일:

- `Builds/Releases/StabInTheBack-Windows-x64-v1.0.0.zip`
- 크기: 69,231,520 bytes
- SHA-256: `CF981FD75E0837663F8846DFDCC69AC122913C4454A95F9D5516B326F06E3226`

이 ZIP은 코드·씬·로컬 기동 검증 산출물이다. 위 Cloud 연결을 확인하고 다시 빌드하기 전에는 Relay/Vivox 포함 최종 배포본으로 간주하지 않는다.

검증 로그:

- `Logs/stage4-build.log`
- `Logs/stage4-release-build.log`
- `Logs/windows-smoke.log`

## 6. 실제 멀티플레이 최종 확인표

자동 검증은 실제 사람의 동시 입력, Relay 지연, Vivox 음성 품질까지 보장하지 않는다. 배포 전 아래 실기 테스트를 한 번 수행한다.

### MPPM 역할

- Main Editor: `Client and Server`(호스트)
- Player 2~4: `Client`

P2P 호스트 방식이므로 서버 전용 인스턴스가 아니라 호스트 한 명이 Client와 Server를 함께 맡는다.

### 인원별 체크

- 1인: 순차 발판·순차 레버 보조가 작동하고 끝까지 진행되는지
- 2인: 같은 사람이 두 발판 또는 두 레버를 독점할 때 통과되지 않는지
- 4인: 분산 단서 전달, 마검 2개 희소성, 원킬과 사망 드롭, 전원 집결 인원수가 맞는지
- 중도 참가: 이미 열린 문과 현재 HUD 단계가 맞게 보이는지
- 접속 종료: 발판·집결 인원에서 즉시 빠지고 남은 인원 기준으로 복구되는지
- 음성: 거리 감쇠, Push-to-Talk/토글, 음소거 상태가 ESC 옵션과 일치하는지
- 종료 흐름: 성공 후 Lobby 복귀, ESC의 메인 메뉴 복귀, 재접속이 정상인지

## 7. 다음 개선 후보

- 실제 4인 플레이 로그를 바탕으로 쌍레버 7초와 발판 2.5초를 조정한다.
- 분산 단서를 더 숨기거나 매 판 룬 순서를 바꾸는 변형을 추가한다.
- 낙사 구간은 Relay 지연 검증 뒤 체크포인트와 함께 확장한다.
- 마검 보관실의 시각적 은폐와 가짜 보관함을 추가해 탐색·심리전을 강화한다.

## 8. 재생성 및 배포 명령

Unity 메뉴에서 `Tools > Stage > Build Stage 4 (Magic Sword Escape)`로 씬을 다시 생성할 수 있다. 전체 배포는 `Tools > Release > Build Windows x64 (출시 빌드)`를 사용한다. ZIP 파일 하나를 배포하면 되며, 실행 파일만 따로 보내면 안 된다.
