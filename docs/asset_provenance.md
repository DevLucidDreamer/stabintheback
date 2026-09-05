# 에셋 출처 및 배포 권리 확인표

배포 전 프로젝트 권리 보유자가 아래 자체 제작 에셋의 원본 또는 사용 허가를 확인해야 한다.
외부 라이선스가 있는 경우 출처 URL, 저작자, 라이선스 버전과 원문 사본을 이 문서에 추가한다.

| 분류 | 경로 | 현재 기록 | 배포 전 확인 |
|---|---|---|---|
| 플레이어 캐릭터 | `Assets/Player/*.fbx` | 프로젝트 제공 파일 | 제작 원본 또는 상업 배포 허가 확인 필요 |
| 캠프 모델 | `Assets/Models/chair.blend`, `Assets/Models/TENT.blend` | 프로젝트 제공 파일 | 제작 원본 또는 상업 배포 허가 확인 필요 |
| 무기 모델 | `Assets/Models/weapons/*.blend` | 프로젝트 제공 파일 | 제작 원본 또는 상업 배포 허가 확인 필요 |
| 게임 아이콘 | `Assets/Sprite/GameIcon.png` | 프로젝트 캐릭터·냉동참치 참조로 생성 | 원본 캐릭터/모델 권리 및 생성 서비스 약관 확인 필요 |

코드·폰트·오디오 고지 사항은 `Assets/StreamingAssets/THIRD_PARTY_NOTICES.txt`에 기록되어 배포 ZIP에 포함된다.

## 2026-09-05 · 돌 신전

- `Assets/Models/성배병.blend`: 사용자가 제공한 원본. Blender에서 평가된 메시를 내보내 Unity 전용 메시·프리팹으로 변환했다. 원본은 변경하지 않았다.
- 신전 구조와 실제 크기에 맞춘 UV 메시: 이번 작업에서 작성한 기본 메시 기반 구조물.
- `Assets/Textures/Temple/*.jpg`: ambientCG의 [Bricks089](https://ambientcg.com/view?id=Bricks089), [PavingStones136](https://ambientcg.com/view?id=PavingStones136). 2K 색상·노멀·AO 텍스처. [CC0 라이선스](https://docs.ambientcg.com/license/), 2026-09-05 다운로드. `Temple_*.mat`에 적용했으며 배포 고지에도 출처를 기록했다.
- `Assets/Resources/Audio/SFX/player_impact_*.ogg`: [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds), CC0. 원문 라이선스는 `Assets/StreamingAssets/Licenses/Kenney_Impact_CC0.txt`에 보관했다.
- `Assets/Resources/Audio/Music/*.ogg`: FazeDevWater의 [Dungeon Themes](https://opengameart.org/content/dungeon-themes), CC0. 원본 파일명과 다운로드 날짜는 배포 고지에 기록했다.

## 2·3단계 · 폐광과 제단

- `Assets/Prefabs/Weapons/MineLantern.prefab`: 이번 작업에서 직접 만든 Unity 기본 메시 조합. 철제 프레임·손잡이·발광 코어·휴대 조명 포함.
- 조각상, 돌다리, 제단과 구조물: 이번 작업에서 만든 기본 메시 조합. 위 ambientCG CC0 텍스처와 기존 CC0 오디오를 재사용한다.
