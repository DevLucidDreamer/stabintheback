using Mirror;
using UnityEngine;

/// <summary>
/// 클라이언트가 보낸 상호작용 요청을 서버에서 다시 검증하는 공통 유틸리티.
/// 화면 레이캐스트와 트리거 판정은 편의를 위한 클라이언트 UX일 뿐, 권한 판정에는 사용하지 않는다.
/// </summary>
public static class ServerInteractionGuard
{
    public const float DefaultRange = 3.75f;

    public static bool HasPlayer(NetworkConnectionToClient sender)
        => sender != null && sender.identity != null && sender.identity.gameObject.activeInHierarchy;

    public static bool IsNear(NetworkConnectionToClient sender, Vector3 target, float range = DefaultRange)
    {
        if (!HasPlayer(sender) || !IsFinite(target))
            return false;

        Vector3 delta = sender.identity.transform.position - target;
        return delta.sqrMagnitude <= range * range;
    }

    public static bool IsInside(NetworkConnectionToClient sender, Collider area, float padding = 0.2f)
    {
        if (!HasPlayer(sender) || area == null || !area.enabled)
            return false;

        Vector3 player = sender.identity.transform.position + Vector3.up * 0.5f;
        Vector3 closest = area.ClosestPoint(player);
        return (closest - player).sqrMagnitude <= padding * padding;
    }

    public static bool IsFinite(Vector3 value)
        => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
}
