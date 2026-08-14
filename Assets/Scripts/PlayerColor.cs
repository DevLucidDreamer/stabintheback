using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어 캐릭터의 색. 서버가 접속 순서대로 겹치지 않게 하나씩 배정하고,
/// 클라이언트는 받은 번호대로 모델의 머티리얼을 갈아끼운다.
///
/// 색은 <see cref="PlayerColorPalette"/>에 머티리얼 묶음으로 들어 있다.
/// 모델 자체를 바꾸는 게 아니라 머티리얼만 바꾸므로 Avatar·AnimatorController는 그대로다.
///
/// 1인칭이라 본인은 자기 몸이 보이지 않지만(RemoteAvatar가 꺼져 있다) 색은 그대로 칠해 둔다 —
/// 남에게 보이는 몸과 죽었을 때 남는 시체가 같은 색이어야 하기 때문이다.
/// </summary>
public class PlayerColor : NetworkBehaviour
{
    [Tooltip("색 목록. Tools > Player > Setup Player Colors 가 만들어 연결한다")]
    [SerializeField] private PlayerColorPalette palette;

    [Tooltip("색을 칠할 모델의 루트. 비워 두면 RemoteAvatar를 찾아 쓴다")]
    [SerializeField] private Transform modelRoot;

    [SyncVar(hook = nameof(OnColorChanged))]
    private int colorIndex = -1;

    /// <summary>배정된 색 번호. 아직 배정 전이면 -1.</summary>
    public int ColorIndex => colorIndex;

    public string ColorName
    {
        get
        {
            PlayerColorPalette.Variant variant = palette != null ? palette.Get(colorIndex) : null;
            return variant != null ? variant.displayName : string.Empty;
        }
    }

    /// <summary>대기실 이름표 같은 데 쓸 대표 색.</summary>
    public Color UiColor
    {
        get
        {
            PlayerColorPalette.Variant variant = palette != null ? palette.Get(colorIndex) : null;
            return variant != null ? variant.uiColor : Color.white;
        }
    }

    public override void OnStartServer()
    {
        int size = palette != null ? palette.Count : 0;
        if (size == 0)
        {
            Debug.LogWarning("[PlayerColor] 색 목록이 비어 있습니다. " +
                             "'Tools > Player > Setup Player Colors'를 먼저 실행하세요.", this);
            return;
        }

        colorIndex = PlayerColorAssigner.Assign(ConnectionKey(), size, this);
    }

    public override void OnStartClient() => Paint(ModelRoot(), colorIndex);

    private void OnColorChanged(int oldIndex, int newIndex) => Paint(ModelRoot(), newIndex);

    /// <summary>
    /// 다른 계층(죽었을 때 남는 시체 등)에도 같은 색을 칠한다.
    /// 시체 프리팹은 같은 goshi 모델로 만들어서 렌더러 구성이 일치한다.
    /// </summary>
    public void ApplyTo(Transform root) => Paint(root, colorIndex);

    private Transform ModelRoot()
    {
        if (modelRoot != null)
            return modelRoot;

        // 애니메이션 셋업을 다시 돌리면 RemoteAvatar가 통째로 새로 만들어져 참조가 끊긴다.
        // 그래서 이름으로 찾아 스스로 복구한다.
        return transform.Find("RemoteAvatar");
    }

    private void Paint(Transform root, int index)
    {
        if (root == null)
            return;

        PlayerColorPalette.Variant variant = palette != null ? palette.Get(index) : null;
        if (variant == null || variant.renderers == null)
            return;

        Renderer[] targets = root.GetComponentsInChildren<Renderer>(true);
        if (targets.Length != variant.renderers.Length)
        {
            Debug.LogWarning($"[PlayerColor] 렌더러 개수가 맞지 않아 색을 칠하지 않았습니다 " +
                             $"(모델 {variant.renderers.Length}개 / 대상 {targets.Length}개). " +
                             "모델을 바꿨다면 'Tools > Player > Setup Player Colors'를 다시 실행하세요.", this);
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            Material[] materials = variant.renderers[i].materials;
            if (materials != null && materials.Length > 0)
                targets[i].sharedMaterials = materials;
        }
    }

    /// <summary>씬이 바뀌어 다시 스폰돼도 같은 색을 주기 위한 열쇠.</summary>
    private int ConnectionKey()
        => connectionToClient != null ? connectionToClient.connectionId : -(int)netId;
}
