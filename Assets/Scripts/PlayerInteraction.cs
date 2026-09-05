using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마우스 상호작용 담당.
/// - 화면 중앙 레이캐스트로 Interactable을 조준
/// - 좌클릭: 빈손이면 상호작용(줍기/열기), 무기를 들고 있으면 스윙
/// - 우클릭: 무기를 든 채로도 서랍/냉장고 등 조작
/// - G: 든 무기 내려놓기
///
/// 멀티플레이(WeaponNetworkManager 존재)면 줍기/버리기/스윙을 서버 권한으로 요청하고,
/// 시각 반영(손에 붙이기/스윙 애니메이션)은 매니저가 모든 클라이언트에서 호출한다.
/// 스윙 시 판정(넉백)은 각 클라이언트가 로컬로 수행한다(공격 판정 도입, Phase 4).
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("상호작용")]
    [Tooltip("상호작용 가능한 최대 거리 (m)")]
    [SerializeField] private float interactRange = 3f;

    [Header("무기")]
    [Tooltip("무기를 드는 위치 (카메라 기준 오프셋)")]
    [SerializeField] private Vector3 holdSocketPosition = new Vector3(0.35f, -0.35f, 0.7f);
    [Tooltip("스윙 한 번에 걸리는 시간(초)")]
    [SerializeField] private float swingDuration = 0.45f;

    [Header("스윙 판정")]
    [Tooltip("스윙이 닿는 앞쪽 거리 (m)")]
    [SerializeField] private float swingReach = 1.4f;
    [Tooltip("타격 판정 반경 (m)")]
    [SerializeField] private float swingRadius = 0.9f;
    [Tooltip("맞은 Rigidbody에 가하는 넉백 세기")]
    [SerializeField] private float swingForce = 6f;

    /// <summary>스윙이 무언가를 맞혔을 때(향후 데미지 시스템 연결점).</summary>
    public static event Action<PlayerInteraction, Collider> OnSwingHit;

    private Camera cam;
    private Transform holdSocket;   // 카메라 자식으로 만드는 '손' 소켓
    private Weapon heldWeapon;
    private Interactable target;    // 현재 조준 중인 대상
    private bool wasLocked;
    private float swingTimer = -1f; // 0 이상이면 스윙 진행 중
    private bool swingHitDone;      // 이번 스윙에서 판정을 이미 했는지
    private bool inputEnabled = true;

    private Mirror.NetworkIdentity identity;
    private bool identityCached;

    public Transform HandSocket => holdSocket;
    public Weapon HeldWeapon => heldWeapon;

    private void Start()
    {
        cam = GetComponentInChildren<Camera>(true);
        holdSocket = new GameObject("HoldSocket").transform;
        if (cam != null)
            holdSocket.SetParent(cam.transform, false);
        holdSocket.localPosition = holdSocketPosition;
    }

    private void Update()
    {
        // 스윙 애니메이션/판정은 원격 플레이어에게도 보여야 하므로 입력 게이트 밖에서 갱신.
        UpdateSwing();

        if (!inputEnabled)
            return;

        bool locked = Cursor.lockState == CursorLockMode.Locked;
        bool justLocked = locked && !wasLocked; // 커서 재잠금 클릭이 상호작용으로 새지 않게
        wasLocked = locked;

        if (!locked)
        {
            target = null;
            return;
        }

        FindTarget();

        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;
        if (mouse == null)
            return;

        if (mouse.leftButton.wasPressedThisFrame && !justLocked)
        {
            if (heldWeapon != null)
                RequestSwing();
            else if (target != null)
                target.Interact(this);
        }

        // 무기를 들고 있어도 우클릭으로는 서랍/문을 열 수 있다.
        if (mouse.rightButton.wasPressedThisFrame && target != null)
            target.Interact(this);

        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) > .01f) WeaponNetworkManager.Instance?.RequestCycle(wheel > 0 ? -1 : 1);

        if (kb != null && kb.gKey.wasPressedThisFrame)
            Drop();
    }

    /// <summary>
    /// HUD 갱신은 LateUpdate에서 한다.
    /// Update는 조준이 풀렸을 때 중간에 빠져나가므로, 거기 두면 문구가 남아 버린다.
    /// </summary>
    private void LateUpdate() => UpdateHud();

    private void OnDisable()
    {
        if (!IsLocalView())
            return;

        GameHud hud = GameHud.Current;
        if (hud == null)
            return;

        hud.SetCrosshair(false);
        hud.SetPrompt(string.Empty);
        hud.SetBottom(string.Empty);
    }

    /// <summary>화면 중앙에서 레이캐스트하여 조준 중인 Interactable을 찾는다.</summary>
    private void FindTarget()
    {
        target = null;
        if (cam == null)
            return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            target = hit.collider.GetComponentInParent<Interactable>();
    }

    // ------------------------------------------------------------ 무기 줍기/버리기 (입력 → 요청)

    public void PickUp(Weapon weapon)
    {
        var mgr = WeaponNetworkManager.Instance;
        if (mgr != null)
        {
            mgr.RequestPickup(weapon.WeaponId); // 시각 반영은 매니저가 전 클라에 지시
            return;
        }

        // 싱글플레이
        if (heldWeapon != null)
            Drop();
        AttachWeaponVisual(weapon);
    }

    public void Drop()
    {
        if (heldWeapon == null || cam == null)
            return;

        Vector3 dropPos = cam.transform.position + cam.transform.forward * 1f;
        Quaternion dropRot = heldWeapon.transform.rotation;

        var mgr = WeaponNetworkManager.Instance;
        if (mgr != null)
        {
            mgr.RequestDrop(heldWeapon.WeaponId, dropPos, dropRot); // ApplyFree가 실제 detach + 정리
            return;
        }

        // 싱글플레이
        Weapon w = heldWeapon;
        heldWeapon = null;
        swingTimer = -1f;
        holdSocket.localRotation = Quaternion.identity;
        w.DetachTo(dropPos, dropRot, true);
    }

    private void RequestSwing()
    {
        var mgr = WeaponNetworkManager.Instance;
        if (mgr != null)
            mgr.RequestSwing();
        else
            PlaySwingVisual();
    }

    // ------------------------------------------------------------ 시각 반영 (매니저가 전 클라에서 호출)

    public void AttachWeaponVisual(Weapon weapon)
    {
        if (weapon == null || holdSocket == null)
            return;
        weapon.AttachTo(holdSocket);
        heldWeapon = weapon;
        swingTimer = -1f; holdSocket.localRotation = Quaternion.identity;
        if (IsLocalView()) GameHud.Ensure().ShowWeaponName(weapon.DisplayName);
        GameAudio.PlayAt("pickup", transform.position + Vector3.up, 0.28f,
            UnityEngine.Random.Range(0.96f, 1.04f), 1.2f, 10f);
    }

    public void StoreWeaponVisual(Weapon weapon)
    {
        if (heldWeapon == weapon)
        {
            heldWeapon = null; swingTimer = -1f;
            if (holdSocket != null) holdSocket.localRotation = Quaternion.identity;
        }
        if (holdSocket != null) weapon.transform.SetParent(holdSocket, false);
        weapon.gameObject.SetActive(false);
    }

    public void DetachWeaponVisual(Weapon weapon, Vector3 worldPos, Quaternion worldRot, bool snapToGround = true)
    {
        if (weapon == null)
            return;
        weapon.DetachTo(worldPos, worldRot, false, snapToGround);
        if (heldWeapon == weapon)
        {
            heldWeapon = null;
            swingTimer = -1f;
            if (holdSocket != null)
                holdSocket.localRotation = Quaternion.identity;
        }
    }

    public void PlaySwingVisual()
    {
        if (swingTimer < 0f)
        {
            swingTimer = 0f;
            swingHitDone = false;
            string clip = UnityEngine.Random.value < 0.5f ? "weapon_swing_01" : "weapon_swing_02";
            GameAudio.PlayAt(clip, transform.position + Vector3.up * 1.2f, 0.34f,
                UnityEngine.Random.Range(0.93f, 1.07f), 1.2f, 15f);
        }
    }

    // ------------------------------------------------------------ 스윙 연출 + 판정

    /// <summary>손 소켓을 준비→휘두르기→복귀 순서로 회전. 내려치는 순간 한 번 판정한다.</summary>
    private void UpdateSwing()
    {
        if (swingTimer < 0f || holdSocket == null)
            return;

        swingTimer += Time.deltaTime;
        float t = swingTimer / swingDuration;

        // 내려치기 구간 진입 시 1회 판정.
        if (!swingHitDone && t >= 0.45f)
        {
            swingHitDone = true;
            DoSwingHit();
        }

        if (t >= 1f)
        {
            swingTimer = -1f;
            holdSocket.localRotation = Quaternion.identity;
            return;
        }

        Quaternion idle = Quaternion.identity;
        Quaternion windup = Quaternion.Euler(-35f, 30f, 10f);  // 뒤로 들어올리기
        Quaternion strike = Quaternion.Euler(65f, -25f, -10f); // 내려치기

        Quaternion rot;
        if (t < 0.3f)
            rot = Quaternion.Slerp(idle, windup, Mathf.SmoothStep(0f, 1f, t / 0.3f));
        else if (t < 0.65f)
            rot = Quaternion.Slerp(windup, strike, Mathf.SmoothStep(0f, 1f, (t - 0.3f) / 0.35f));
        else
            rot = Quaternion.Slerp(strike, idle, Mathf.SmoothStep(0f, 1f, (t - 0.65f) / 0.35f));

        holdSocket.localRotation = rot;
    }

    /// <summary>
    /// 스윙 판정: 앞쪽 구체 범위 내 Rigidbody에 넉백을 준다.
    /// 각 클라이언트가 자기 로컬 물체에 대해 수행한다. (데미지 시스템은 아직 없음)
    /// </summary>
    private void DoSwingHit()
    {
        Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * swingReach;
        bool hitSomething = false;

        foreach (Collider col in Physics.OverlapSphere(origin, swingRadius))
        {
            if (col.isTrigger || col.transform.IsChildOf(transform))
                continue; // 자기 몸/자기 무기 제외

            // Player impacts are emitted once by the server after a confirmed hit.
            if (col.GetComponentInParent<PlayerHealth>() != null)
                continue;

            hitSomething = true;

            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                Vector3 dir = rb.worldCenterOfMass - transform.position;
                dir.y = 0f;
                dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
                rb.AddForce(dir * swingForce + Vector3.up * 2f, ForceMode.Impulse);
            }

            OnSwingHit?.Invoke(this, col);
        }

        if (hitSomething)
            GameAudio.PlayAt("weapon_hit", origin, 0.42f,
                UnityEngine.Random.Range(0.92f, 1.06f), 1.2f, 16f);
    }

    // ------------------------------------------------------------ HUD (조준점 + 안내)

    /// <summary>
    /// 조준점과 안내 문구를 HUD에 반영한다.
    /// 내 화면에만 그려야 하므로 로컬 플레이어일 때만 손댄다 —
    /// 다른 사람의 플레이어 오브젝트도 같은 스크립트를 들고 있기 때문이다.
    /// </summary>
    private void UpdateHud()
    {
        if (!IsLocalView())
            return;

        GameHud hud = GameHud.Ensure();
        bool active = inputEnabled && Cursor.lockState == CursorLockMode.Locked;

        hud.SetCrosshair(active);

        if (!active)
        {
            hud.SetPrompt(string.Empty);
            hud.SetBottom(string.Empty);
            return;
        }

        hud.SetPrompt(BuildPrompt());
        // 전체 조작법은 ESC 메뉴에서 확인한다. 플레이 화면에는 조준 대상 안내만 남긴다.
        hud.SetBottom(string.Empty);
    }

    private string BuildPrompt()
    {
        if (heldWeapon == null)
            return target != null ? "[좌클릭] " + target.GetPrompt() : string.Empty;

        // 무기를 들고 있으면 좌클릭은 스윙에 묶인다. 조작은 우클릭으로 안내한다.
        return target != null
            ? "[우클릭] " + target.GetPrompt()
            : string.Empty;
    }

    /// <summary>이 컴포넌트가 내 화면을 담당하는가(싱글플레이면 항상 참).</summary>
    private bool IsLocalView()
    {
        // 매 프레임 부르므로 컴포넌트는 한 번만 찾아 둔다.
        if (!identityCached)
        {
            identity = GetComponent<Mirror.NetworkIdentity>();
            identityCached = true;
        }

        return identity == null || identity.isLocalPlayer;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
        {
            target = null;
            if (IsLocalView())
            {
                GameHud hud = GameHud.Current;
                if (hud != null)
                {
                    hud.SetCrosshair(false);
                    hud.SetPrompt(string.Empty);
                }
            }
        }
    }
}
