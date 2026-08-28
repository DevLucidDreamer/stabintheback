# Unity Relay + Mirror 설정

이 프로젝트의 게임 동기화는 Mirror를 유지하고, 인터넷 전송 계층만 `RelayMirrorTransport`(Unity Transport + Unity Relay)로 구성한다. Vivox 3D 음성 채널은 Mirror 플레이어가 접속한 뒤 별도로 참가하므로 Relay와 함께 동작한다.

## Unity Dashboard에서 한 번만 할 일

1. 프로젝트가 현재 Unity Cloud Project에 연결되어 있는지 확인한다.
2. Unity Dashboard에서 **Multiplayer > Relay**를 활성화한다.
3. 요금제/결제 설정이나 서비스 약관 확인이 표시되면 완료한다.
4. Vivox도 같은 Cloud Project에서 활성화되어 있어야 한다.

프로젝트에는 Multiplayer Services, Authentication, Unity Transport, Vivox 패키지가 이미 설치되어 있다. 비밀 키를 클라이언트에 넣지 않으며, 실행 시 익명 인증으로 Relay 할당을 만든다.

## 실행 흐름

- **방 열기**: 익명 로그인 → Relay 할당 생성 → 참가 코드 발급 → Mirror Host 시작
- **코드로 참가**: 익명 로그인 → 참가 코드로 Relay 할당 참가 → Mirror Client 시작
- **빠른 참가**: 같은 LAN에서 방 코드를 검색하되 실제 게임 연결은 Relay 사용
- **Vivox**: Mirror 접속 및 플레이어 스폰 후 같은 위치 음성 채널에 참가

Relay 참가 코드는 호스트가 방을 닫거나 할당이 만료되면 사용할 수 없다. 호스트가 종료되면 현재 구조에서는 방도 종료되며 자동 호스트 이전은 하지 않는다.

## 두 PC 점검

1. 서로 다른 네트워크의 Windows PC 두 대에서 같은 빌드를 실행한다.
2. PC A에서 **방 열기**를 누르고 로비의 코드를 확인한다.
3. PC B에서 해당 코드를 입력해 참가한다.
4. 이동 동기화와 로비 인원 수를 확인한다.
5. 마이크 권한을 허용한 뒤 가까이/멀리 이동하며 Vivox 음량 감쇠를 확인한다.

에디터에서 Lobby 씬을 직접 열어 Mirror HUD로 Host를 누르는 방식은 Relay 할당을 만들지 않으므로 지원하지 않는다. 반드시 MainTitle의 방 열기/참가 흐름으로 테스트한다.
