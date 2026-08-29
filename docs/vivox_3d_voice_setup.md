# Vivox 3D 근접 음성 채팅

`NetworkPlayer`가 스폰되면 로컬 플레이어만 Vivox에 로그인하고 서버가 발급한 세션 전용 3D 채널에 참가한다. 플레이어 카메라의 위치와 방향은 0.1초마다 Vivox에 전달된다.

## 현재 기본값

- 오픈 마이크
- 원음 거리: 1 Unity unit
- 최대 가청 거리: 25 Unity units
- 거리 감쇠: `InverseByDistance`
- 좌우 방향감 활성화
- Dedicated Server 빌드에서는 Vivox 비활성화

설정은 `Assets/Scripts/VivoxProximityVoice.cs`의 직렬화 필드에서 바꿀 수 있다. 컴포넌트를 `NetworkPlayer` 프리팹에 미리 추가하면 Inspector에서 값을 조정할 수 있고, 없어도 런타임에 자동으로 추가된다.

PTT가 필요하면 `pushToTalk`을 켠다. 기본 키는 `V`다. 오픈 마이크에서는 화면 오른쪽 아래의 마이크 UI 또는 `M` 키로 음소거를 켜고 끈다. UI에는 연결 중·연결됨·음소거·연결 실패 상태가 표시된다.

## Unity Dashboard 확인

프로젝트는 `com.unity.services.vivox` 16.11.0과 Vivox 환경값이 이미 설정되어 있다. 실제 빌드 테스트 전에 Unity Dashboard에서 이 Unity 프로젝트의 Vivox 서비스가 활성화되어 있는지 확인한다. 클라이언트는 UGS 익명 인증을 사용하므로 별도의 계정 UI는 필요 없다.

## 테스트

1. 서로 다른 PC에서 호스트와 클라이언트로 같은 Mirror 방에 접속한다.
2. 두 OS 모두 게임의 마이크 권한을 허용한다.
3. Unity Console에서 `[Vivox] 3D 음성 채널 연결 완료` 로그를 확인한다.
4. 플레이어 사이를 1m, 10m, 25m 이상으로 바꾸며 볼륨과 방향감을 확인한다.

같은 PC에서 Editor와 빌드를 동시에 실행하면 UGS 익명 계정 세션이 겹칠 수 있다. 이 경우 Multiplayer Play Mode의 서로 분리된 플레이어 환경이나 서로 다른 PC 두 대를 사용하는 것이 가장 확실하다.
