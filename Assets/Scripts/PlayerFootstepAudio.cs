using UnityEngine;

/// <summary>로컬 입력과 네트워크 이동 모두에서 실제 이동 거리를 기준으로 발소리를 낸다.</summary>
public class PlayerFootstepAudio : MonoBehaviour
{
    private static readonly string[] Footsteps =
    {
        "footstep_01", "footstep_02", "footstep_03", "footstep_04"
    };

    private Vector3 previousPosition;
    private float distanceSinceStep;
    private int previousClip = -1;
    private bool initialized;

    private void LateUpdate()
    {
        Vector3 current = transform.position;
        if (!initialized)
        {
            previousPosition = current;
            initialized = true;
            return;
        }

        Vector3 delta = current - previousPosition;
        previousPosition = current;
        delta.y = 0f;

        float moved = delta.magnitude;
        if (moved > 1.5f) // 텔레포트나 첫 네트워크 보정은 발걸음으로 세지 않는다.
        {
            distanceSinceStep = 0f;
            return;
        }

        float speed = moved / Mathf.Max(Time.deltaTime, 0.0001f);
        bool grounded = Physics.Raycast(current + Vector3.up * 0.25f, Vector3.down, 1.35f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (!grounded || speed < 0.35f)
            return;

        distanceSinceStep += moved;
        float stride = speed > 7f ? 1.65f : 2.05f;
        if (distanceSinceStep < stride)
            return;

        distanceSinceStep %= stride;
        int index = Random.Range(0, Footsteps.Length - 1);
        if (previousClip >= 0 && index >= previousClip)
            index++;
        previousClip = index;

        GameAudio.PlayAt(Footsteps[index], current + Vector3.up * 0.08f, 0.2f,
            Random.Range(0.94f, 1.06f), 1.2f, 12f);
    }
}
