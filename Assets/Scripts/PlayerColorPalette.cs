using UnityEngine;

/// <summary>
/// 플레이어에게 배정할 캐릭터 색 목록.
///
/// 색깔별 goshi 모델(goshi(red).fbx 등)은 메시와 뼈대가 전부 같고 머티리얼 색만 다르다.
/// 그래서 모델을 통째로 갈아끼우지 않고 <b>머티리얼만 갈아끼운다</b> — 프리팹 구조도,
/// Avatar도, AnimatorController도 건드릴 필요가 없어 애니메이션이 깨질 여지가 없다.
///
/// 애셋은 'Tools > Player > Setup Player Colors'가 모델에서 읽어 굽는다. 직접 채우지 않아도 된다.
/// </summary>
[CreateAssetMenu(menuName = "Stab in the Back/Player Color Palette", fileName = "PlayerColors")]
public class PlayerColorPalette : ScriptableObject
{
    /// <summary>렌더러 하나가 쓰는 머티리얼들(서브메시 순서 그대로).</summary>
    [System.Serializable]
    public class RendererSlot
    {
        public Material[] materials;
    }

    [System.Serializable]
    public class Variant
    {
        [Tooltip("화면에 보여줄 이름")]
        public string displayName = "기본";

        [Tooltip("대기실·UI에서 이 색을 나타낼 대표 색")]
        public Color uiColor = Color.white;

        [Tooltip("모델의 렌더러를 계층 순서대로 훑어 담은 머티리얼 묶음")]
        public RendererSlot[] renderers;

        [Tooltip("이 색을 뽑아낸 모델 파일. 참고용")]
        public string sourceModel;
    }

    [SerializeField] private Variant[] variants = new Variant[0];

    public int Count => variants != null ? variants.Length : 0;

    public Variant Get(int index)
        => variants != null && index >= 0 && index < variants.Length ? variants[index] : null;

    /// <summary>빌더 전용. 손으로 부르지 말 것.</summary>
    public void SetVariants(Variant[] value) => variants = value;
}
