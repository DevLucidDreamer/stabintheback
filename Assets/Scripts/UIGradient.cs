using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 그래픽에 두 색 그라데이션을 입힌다.
/// 타이틀 화면 왼쪽을 어둡게 깔아 글씨가 배경 위에서도 읽히게 하는 데 쓴다.
///
/// 그라데이션 텍스처를 애셋으로 굽지 않고 정점 색만 바꾸므로 파일이 늘지 않는다.
/// (Image 하나는 정점이 4개라 딱 선형 그라데이션이 된다)
/// </summary>
[AddComponentMenu("UI/Effects/UI Gradient")]
[RequireComponent(typeof(Graphic))]
public class UIGradient : BaseMeshEffect
{
    public enum Direction { Horizontal, Vertical }

    [Tooltip("Horizontal이면 왼쪽→오른쪽, Vertical이면 아래→위")]
    public Direction direction = Direction.Horizontal;

    [Tooltip("시작 색(왼쪽/아래)")]
    public Color from = new Color(0f, 0f, 0f, 0.85f);

    [Tooltip("끝 색(오른쪽/위)")]
    public Color to = new Color(0f, 0f, 0f, 0f);

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
            return;

        Rect rect = graphic.rectTransform.rect;
        UIVertex vertex = default;

        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);

            float t = direction == Direction.Horizontal
                ? Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x)
                : Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);

            vertex.color = Color.Lerp(from, to, t);
            vh.SetUIVertex(vertex, i);
        }
    }
}
