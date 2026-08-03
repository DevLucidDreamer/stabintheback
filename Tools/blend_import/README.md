# blend_import — .blend 메시를 Unity로 가져오기

Unity가 `.blend`를 임포트하려면 **그 PC에 Blender가 설치돼 있어야** 한다.
Unity가 Blender를 직접 호출해 FBX로 바꾸기 때문이다. 설치돼 있지 않으면
`.blend`는 모델로 임포트되지 않고 썸네일만 만들어진다
(`Logs/AssetImportWorker*.log`에 `ModelImporter`가 아니라 `PreviewImporter`로 찍힌다).

이 폴더의 스크립트는 Blender 없이 `.blend` 파일을 직접 읽어서
Unity가 쓸 수 있는 JSON으로 바꾼다.

- `blendparse.py` — zstd 압축을 풀고, Blender 5.0의 새 헤더(`BLENDER17-01v0502`,
  32바이트 BHead)와 SDNA 타입 정보를 해석한다.
- `blend2json.py` — Blender 5.0의 새 `AttributeStorage`에서 메시를 꺼내
  (`position`, `.corner_vert`, `material_index`, `UVMap`, `sharp_face`)
  Unity 좌표계로 변환해 저장한다.

## 실행

```bash
python Tools/blend_import/blend2json.py Assets/Models/weapons Assets/Models/weapons/Converted
```

**Python 3.14 이상**이 필요하다 (zstd 압축 해제에 표준 라이브러리 `compression.zstd`를 쓴다).
Windows에서 `python`이 Microsoft Store 안내창만 띄운다면 Python이 실제로 설치돼 있지 않은 것이니,
python.org에서 설치한 뒤 다시 실행하면 된다.

변환 결과(`Assets/Models/weapons/Converted/*.mesh.json`)는 저장소에 함께 들어 있으므로,
**모델을 새로 고치지 않는 한 이 스크립트를 돌릴 일은 없다.**

## 그 다음

Unity 에디터에서 `Tools > Weapons > Build Weapon Prefabs`를 실행하면
JSON에서 Mesh/Material 애셋과 무기 프리팹(`Assets/Prefabs/Weapons/`)을 만든다.

## 한계

정적 메시 전용이다. 아마추어(뼈대)·스키닝·애니메이션은 읽지 못한다.
머티리얼은 노드 그래프가 아니라 뷰포트 표시 색을 가져온다 — 지금 무기들처럼
단색으로 칠한 로우폴리 모델에는 이걸로 충분하다.

플레이어 캐릭터(`Assets/Player/goshi(final!).blend`)처럼 리깅된 모델은
이 방식으로 가져올 수 없어서, 같은 캐릭터를 내보내 둔 `goshi(final).fbx`를 쓴다.
Blender를 설치하면 `GoshiModel.Load()`가 알아서 `.blend` 쪽을 먼저 집는다.
