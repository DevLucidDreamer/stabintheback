using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>캠핑장 한 판의 진행 단계.</summary>
public enum CampPhase
{
    /// <summary>낮 — 캠핑장을 뒤져 재료를 모은다.</summary>
    Gathering = 0,

    /// <summary>재료를 다 모아 해가 넘어가는 중.</summary>
    Dusk = 1,

    /// <summary>저녁노을 — 화로에 불을 피우고 바베큐를 굽는다.</summary>
    Cooking = 2,

    /// <summary>다 구웠다. 승리 연출 후 대기실로 돌아간다.</summary>
    Feast = 3,
}

/// <summary>모아야 할 재료 종류.</summary>
public enum Ingredient
{
    /// <summary>화로에 넣어 불을 피우는 연료.</summary>
    Firewood = 0,

    /// <summary>그릴에 굽는다.</summary>
    Meat = 1,

    /// <summary>그릴에 굽는다.</summary>
    Vegetable = 2,
}

/// <summary>그릴 한 칸의 상태.</summary>
public enum GrillSlotState
{
    Empty = 0,
    Raw = 1,
    Cooked = 2,
    Burnt = 3,
}

/// <summary>
/// 캠핑장 한 판의 목표를 서버 권한으로 관리하는 매니저. 씬에 하나만 둔다.
///
/// 게임 목표
///   1) <b>낮</b> — 캠핑장 곳곳에 흩어진 장작·고기·야채를 팀이 함께 모은다.
///   2) 재료를 전부 모으면 <b>해가 넘어가며 저녁노을</b>이 진다(자동, 되돌아가지 않는다).
///   3) <b>노을</b> — 화로에 장작을 넣어 불을 피우고, 그릴에 고기·야채를 올려 굽는다.
///   4) 전부 구워내면 <b>바베큐 완성</b> — 승리 연출 후 대기실로 돌아간다.
///
/// 재료는 팀 공용 재고다. 아무나 주우면 모두의 재고가 되고, 아무나 그 재고를 꺼내 쓴다.
/// 협동은 재고로 강제하고, 배신은 무기(마검)가 담당한다 —
/// 남이 다 구워 놓은 걸 가로채거나, 굽고 있는 사람을 때려눕히고 자리를 차지할 수 있다.
///
/// 화로와 그릴은 이 매니저를 거쳐 동작한다(각자 NetworkIdentity를 갖지 않는다).
/// 상호작용 오브젝트는 <see cref="Interactable"/>이라 NetworkBehaviour를 겸할 수 없고,
/// 상태를 한 곳에 모아 두면 늦게 접속한 사람에게 넘겨줄 것도 한 덩어리로 끝난다.
///
/// 동기화는 SyncVar(숫자)와 Command/Rpc(배열)로만 한다.
/// SyncList/SyncDictionary는 Mirror 버전에 따라 API가 흔들려서 쓰지 않는다.
/// </summary>
public class CampGameManager : NetworkBehaviour
{
    public static CampGameManager Instance { get; private set; }

    /// <summary>진행 상황이 바뀌면 호출(HUD·클립보드·그릴 연출이 구독해 갱신).</summary>
    public static event Action OnChanged;

    /// <summary>페이즈가 바뀌는 순간 호출(연출 트리거용).</summary>
    public static event Action<CampPhase> OnPhaseEntered;

    [Header("모아야 할 재료 (씬에 놓인 개수로 자동 보정된다)")]
    [Tooltip("불을 피우는 데 필요한 장작. 전부 화로에 넣어야 불이 붙는다")]
    [SerializeField] private int firewoodNeeded = 4;

    [Tooltip("구워야 할 고기")]
    [SerializeField] private int meatNeeded = 4;

    [Tooltip("구워야 할 야채")]
    [SerializeField] private int vegetableNeeded = 3;

    [Header("굽기")]
    [Tooltip("그릴에 동시에 올릴 수 있는 칸 수")]
    [SerializeField] private int grillSlots = 4;

    [Tooltip("올린 재료가 알맞게 익는 데 걸리는 시간(초)")]
    [SerializeField] private float cookSeconds = 9f;

    [Tooltip("다 익은 뒤 이만큼 더 방치하면 탄다(초)")]
    [SerializeField] private float burnSeconds = 11f;

    [Header("연출")]
    [Tooltip("재료를 다 모은 뒤 해가 넘어가는 데 걸리는 시간(초)")]
    [SerializeField] private float duskSeconds = 9f;

    [Tooltip("바베큐 완성 후 대기실로 돌아가기까지의 시간(초)")]
    [SerializeField] private float feastSeconds = 9f;

    [Tooltip("한 판이 끝나면 돌아갈 씬")]
    [SerializeField] private string lobbyScene = "Lobby";

    // ---- 동기화 상태 -------------------------------------------------------

    [SyncVar] private int phase;                 // CampPhase
    [SyncVar] private double phaseEndsAt = -1d;  // NetworkTime 기준. 음수면 대기 없음

    [SyncVar] private int firewoodHave;          // 주운 장작
    [SyncVar] private int meatHave;
    [SyncVar] private int vegetableHave;

    [SyncVar] private int firewoodLoaded;        // 화로에 넣은 장작
    [SyncVar] private int meatUsed;              // 그릴 위에 올라가 있는 고기
    [SyncVar] private int vegetableUsed;
    [SyncVar] private int cookedCount;           // 잘 익혀 회수한 개수
    [SyncVar] private int burntCount;            // 태워 버린 개수

    // 그릴 칸 — 배열이라 SyncVar로 두지 않고 Rpc로 통째로 보낸다.
    private int[] slotKind;                      // -1 = 빈칸, 그 외 (int)Ingredient
    private double[] slotPlacedAt;               // NetworkTime 기준 올린 시각

    // ---- 로컬 상태 ---------------------------------------------------------

    private readonly Dictionary<int, CollectibleItem> registry = new Dictionary<int, CollectibleItem>();
    private readonly HashSet<int> collectedIds = new HashSet<int>(); // 서버 권한
    private readonly Dictionary<uint, double> nextInteractionAt = new Dictionary<uint, double>();

    private bool serverChangingScene;
    private int lastSignature = int.MinValue;
    private int lastPhase = -1;

    // ---- 조회 -------------------------------------------------------------

    public CampPhase Phase => (CampPhase)phase;
    public bool IsGathering => Phase == CampPhase.Gathering;
    public bool IsCooking => Phase == CampPhase.Cooking;

    /// <summary>화로에 불이 붙었는가. 그릴은 불이 붙어야 쓸 수 있다.</summary>
    public bool FireLit => firewoodLoaded >= firewoodNeeded;

    public int FirewoodNeeded => firewoodNeeded;
    public int MeatNeeded => meatNeeded;
    public int VegetableNeeded => vegetableNeeded;

    public int FirewoodHave => firewoodHave;
    public int MeatHave => meatHave;
    public int VegetableHave => vegetableHave;

    public int FirewoodLoaded => firewoodLoaded;
    public int CookedCount => cookedCount;
    public int BurntCount => burntCount;

    /// <summary>구워내야 하는 총 개수(고기 + 야채).</summary>
    public int CookTarget => meatNeeded + vegetableNeeded;

    /// <summary>아직 그릴에 올리지 않은 고기.</summary>
    public int MeatAvailable => Mathf.Max(0, meatHave - meatUsed);

    /// <summary>아직 그릴에 올리지 않은 야채.</summary>
    public int VegetableAvailable => Mathf.Max(0, vegetableHave - vegetableUsed);

    /// <summary>화로에 더 넣을 수 있는 장작.</summary>
    public int FirewoodAvailable => Mathf.Max(0, firewoodHave - firewoodLoaded);

    public bool GatheringComplete =>
        firewoodHave >= firewoodNeeded && meatHave >= meatNeeded && vegetableHave >= vegetableNeeded;

    /// <summary>
    /// 낮(0) → 저녁노을(1) 진행도. DayNightController가 이 값으로 해를 넘긴다.
    /// </summary>
    public float DuskProgress01
    {
        get
        {
            if (phase <= (int)CampPhase.Gathering)
                return 0f;
            if (phase >= (int)CampPhase.Cooking)
                return 1f;
            if (phaseEndsAt < 0d || duskSeconds <= 0.01f)
                return 1f;

            double left = phaseEndsAt - NetworkTime.time;
            return Mathf.Clamp01(1f - (float)(left / duskSeconds));
        }
    }

    /// <summary>현재 페이즈가 끝나기까지 남은 초. 대기가 없으면 음수.</summary>
    public float PhaseSecondsLeft
        => phaseEndsAt < 0d ? -1f : Mathf.Max(0f, (float)(phaseEndsAt - NetworkTime.time));

    // ---- 수명 주기 ---------------------------------------------------------

    private void Awake()
    {
        Instance = this;
        EnsureSlots();
        BuildRegistry(); // 오프라인/조기 접근 대비
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnStartServer()
    {
        BuildRegistry();
        EnsureSlots();
        ClearSlots();

        collectedIds.Clear();
        nextInteractionAt.Clear();
        serverChangingScene = false;

        phase = (int)CampPhase.Gathering;
        phaseEndsAt = -1d;
        firewoodHave = meatHave = vegetableHave = 0;
        firewoodLoaded = meatUsed = vegetableUsed = 0;
        cookedCount = burntCount = 0;

        // 씬에 실제로 놓인 개수로 목표를 맞춘다.
        // 빌더가 몇 개를 뿌렸든 "다 모으면 저녁이 온다"가 항상 성립하게 하려는 것.
        CountPlacedIngredients();
    }

    public override void OnStartClient()
    {
        BuildRegistry();
        EnsureSlots();
        CmdRequestSync(); // 이미 주워 간 것들 + 그릴 상태를 받아온다
    }

    private void Update()
    {
        if (isServer)
            ServerTick();

        RaiseIfChanged();
    }

    // ---- 재료 획득 ---------------------------------------------------------

    /// <summary>CollectibleItem이 호출한다.</summary>
    public void RequestCollect(int id)
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            OfflineCollect(id);
            return;
        }

        CmdCollect(id);
    }

    [Command(requiresAuthority = false)]
    private void CmdCollect(int id, NetworkConnectionToClient sender = null)
    {
        if (phase != (int)CampPhase.Gathering)
            return; // 노을이 진 뒤에는 더 줍지 않는다
        if (!registry.TryGetValue(id, out CollectibleItem item) || item == null)
            return;
        if (!ServerInteractionGuard.IsNear(sender, item.transform.position) || !ConsumeInteraction(sender))
            return;
        if (!collectedIds.Add(id))
            return; // 이미 누가 가져갔다

        // 종류는 서버가 씬에서 직접 읽는다(클라이언트가 불러 주는 값을 믿지 않는다).
        Add(item.Kind, 1);
        RpcCollected(id);

        if (GatheringComplete)
            ServerEnterDusk();
    }

    [ClientRpc]
    private void RpcCollected(int id) => HideItem(id);

    [Command(requiresAuthority = false)]
    private void CmdRequestSync(NetworkConnectionToClient sender = null)
    {
        if (sender != null)
            TargetSync(sender, collectedIds.ToArray(), slotKind, slotPlacedAt);
    }

    [TargetRpc]
    private void TargetSync(NetworkConnectionToClient target, int[] collected, int[] kinds, double[] times)
    {
        foreach (int id in collected)
            HideItem(id);

        ApplySlots(kinds, times);
    }

    // ---- 화로 -------------------------------------------------------------

    /// <summary>Firepit이 호출한다. 장작을 한 개 넣는다.</summary>
    public void RequestLoadFirewood()
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            if (CanLoadFirewood())
                firewoodLoaded++;
            return;
        }

        CmdLoadFirewood();
    }

    [Command(requiresAuthority = false)]
    private void CmdLoadFirewood(NetworkConnectionToClient sender = null)
    {
        Firepit firepit = FindFirstObjectByType<Firepit>();
        if (firepit == null || !ServerInteractionGuard.IsNear(sender, firepit.transform.position) || !ConsumeInteraction(sender))
            return;
        if (CanLoadFirewood())
            firewoodLoaded++;
    }

    private bool CanLoadFirewood()
        => phase == (int)CampPhase.Cooking && !FireLit && FirewoodAvailable > 0;

    /// <summary>화로를 조준했을 때 보여줄 안내 문구. null이면 상호작용할 게 없다.</summary>
    public string FirepitPrompt()
    {
        switch (Phase)
        {
            case CampPhase.Gathering:
                return "재료를 다 모아야 불을 피운다";
            case CampPhase.Dusk:
                return "해가 넘어가길 기다린다";
            case CampPhase.Feast:
                return "잘 구웠다";
        }

        if (FireLit)
            return "활활 타오른다";
        if (FirewoodAvailable <= 0)
            return $"장작이 없다  ({firewoodLoaded}/{firewoodNeeded})";

        return $"장작 넣기  ({firewoodLoaded}/{firewoodNeeded})";
    }

    /// <summary>지금 화로에 장작을 넣을 수 있는가(프롬프트를 눌러도 되는지).</summary>
    public bool FirepitUsable() => CanLoadFirewood();

    // ---- 그릴 -------------------------------------------------------------

    public int SlotCount => slotKind?.Length ?? 0;

    /// <summary>칸에 올라간 재료. 빈칸이면 false.</summary>
    public bool TryGetSlot(int index, out Ingredient kind)
    {
        kind = Ingredient.Meat;
        if (slotKind == null || index < 0 || index >= slotKind.Length || slotKind[index] < 0)
            return false;

        kind = (Ingredient)slotKind[index];
        return true;
    }

    /// <summary>칸의 익힘 상태.</summary>
    public GrillSlotState SlotState(int index)
    {
        if (slotKind == null || index < 0 || index >= slotKind.Length || slotKind[index] < 0)
            return GrillSlotState.Empty;

        double elapsed = NetworkTime.time - slotPlacedAt[index];
        if (elapsed < cookSeconds)
            return GrillSlotState.Raw;
        if (elapsed < cookSeconds + burnSeconds)
            return GrillSlotState.Cooked;
        return GrillSlotState.Burnt;
    }

    /// <summary>
    /// 칸의 진행도. 0 = 방금 올림, 1 = 다 익음, 그 위로는 타기까지의 여유가 줄어든다.
    /// 게이지 연출에 쓴다.
    /// </summary>
    public float SlotProgress01(int index)
    {
        if (slotKind == null || index < 0 || index >= slotKind.Length || slotKind[index] < 0)
            return 0f;

        double elapsed = NetworkTime.time - slotPlacedAt[index];
        return Mathf.Clamp01((float)(elapsed / Mathf.Max(0.01f, cookSeconds)));
    }

    /// <summary>다 익은 뒤 타기까지 남은 비율(1 = 방금 익음, 0 = 탐).</summary>
    public float SlotBurnLeft01(int index)
    {
        if (SlotState(index) != GrillSlotState.Cooked)
            return 0f;

        double over = NetworkTime.time - slotPlacedAt[index] - cookSeconds;
        return Mathf.Clamp01(1f - (float)(over / Mathf.Max(0.01f, burnSeconds)));
    }

    /// <summary>BarbecueGrill이 호출한다. 상황에 맞는 동작 하나를 서버에 요청한다.</summary>
    public void RequestGrillInteract()
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            if (ServerGrillInteract())
                BroadcastSlotsLocal();
            return;
        }

        CmdGrillInteract();
    }

    [Command(requiresAuthority = false)]
    private void CmdGrillInteract(NetworkConnectionToClient sender = null)
    {
        BarbecueGrill grill = FindFirstObjectByType<BarbecueGrill>();
        if (grill == null || !ServerInteractionGuard.IsNear(sender, grill.transform.position) || !ConsumeInteraction(sender))
            return;
        if (ServerGrillInteract())
            RpcSlots(slotKind, slotPlacedAt);
    }

    [Server]
    private bool ConsumeInteraction(NetworkConnectionToClient sender, double cooldown = 0.12d)
    {
        if (sender?.identity == null)
            return false;
        uint id = sender.identity.netId;
        if (nextInteractionAt.TryGetValue(id, out double next) && NetworkTime.time < next)
            return false;
        nextInteractionAt[id] = NetworkTime.time + cooldown;
        return true;
    }

    /// <summary>
    /// 그릴 상호작용 한 번. 우선순위대로 하나만 처리한다.
    ///   1) 잘 익은 것이 있으면 회수 (가장 급한 일)
    ///   2) 탄 것이 있으면 버린다 (칸을 비워야 다음을 올린다)
    ///   3) 빈칸이 있고 재고가 있으면 올린다
    /// </summary>
    [Server]
    private bool ServerGrillInteract()
    {
        if (phase != (int)CampPhase.Cooking || !FireLit)
            return false;

        // 1) 회수
        for (int i = 0; i < slotKind.Length; i++)
        {
            if (SlotState(i) != GrillSlotState.Cooked)
                continue;

            ClearSlot(i);
            cookedCount++;
            if (cookedCount >= CookTarget)
                ServerEnterFeast();
            return true;
        }

        // 2) 탄 것 버리기 — 재료는 재고로 돌려준다.
        //    재료가 딱 맞아떨어져서, 한 번 태우면 영영 못 깨는 판이 되면 곤란하다.
        for (int i = 0; i < slotKind.Length; i++)
        {
            if (SlotState(i) != GrillSlotState.Burnt)
                continue;

            var kind = (Ingredient)slotKind[i];
            ClearSlot(i);
            burntCount++;
            ReturnToStock(kind);
            return true;
        }

        // 3) 올리기
        for (int i = 0; i < slotKind.Length; i++)
        {
            if (slotKind[i] >= 0)
                continue;
            if (!ServerTakeCookable(out Ingredient kind))
                return false;

            slotKind[i] = (int)kind;
            slotPlacedAt[i] = NetworkTime.time;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 고기를 먼저 소진하고, 없으면 야채를 꺼낸다.
    ///
    /// [Server]를 붙이지 않는다 — Mirror 위버가 끼워 넣는 조기 반환은 out 인자를
    /// 채우지 않고 빠져나간다. 어차피 서버에서만 도는 ServerGrillInteract에서만 부른다.
    /// </summary>
    private bool ServerTakeCookable(out Ingredient kind)
    {
        kind = Ingredient.Meat;

        if (MeatAvailable > 0)
        {
            meatUsed++;
            return true;
        }

        if (VegetableAvailable > 0)
        {
            vegetableUsed++;
            kind = Ingredient.Vegetable;
            return true;
        }

        return false;
    }

    private void ReturnToStock(Ingredient kind)
    {
        if (kind == Ingredient.Meat)
            meatUsed = Mathf.Max(0, meatUsed - 1);
        else if (kind == Ingredient.Vegetable)
            vegetableUsed = Mathf.Max(0, vegetableUsed - 1);
    }

    /// <summary>그릴을 조준했을 때 보여줄 안내 문구.</summary>
    public string GrillPrompt()
    {
        switch (Phase)
        {
            case CampPhase.Gathering:
                return "재료를 다 모아야 굽는다";
            case CampPhase.Dusk:
                return "해가 넘어가길 기다린다";
            case CampPhase.Feast:
                return "바베큐 완성!";
        }

        if (!FireLit)
            return "먼저 화로에 불을 피워라";

        for (int i = 0; i < SlotCount; i++)
            if (SlotState(i) == GrillSlotState.Cooked && TryGetSlot(i, out Ingredient done))
                return $"다 익은 {NameOf(done)} 꺼내기";

        for (int i = 0; i < SlotCount; i++)
            if (SlotState(i) == GrillSlotState.Burnt)
                return "탄 것 치우기";

        bool hasEmpty = false;
        for (int i = 0; i < SlotCount; i++)
            if (slotKind[i] < 0)
            {
                hasEmpty = true;
                break;
            }

        if (!hasEmpty)
            return "굽는 중...";

        if (MeatAvailable > 0)
            return "고기 올리기";
        if (VegetableAvailable > 0)
            return "야채 올리기";

        return "올릴 재료가 없다";
    }

    /// <summary>지금 그릴에 대고 할 수 있는 일이 있는가.</summary>
    public bool GrillUsable()
    {
        if (phase != (int)CampPhase.Cooking || !FireLit)
            return false;

        for (int i = 0; i < SlotCount; i++)
        {
            GrillSlotState state = SlotState(i);
            if (state == GrillSlotState.Cooked || state == GrillSlotState.Burnt)
                return true;
            if (state == GrillSlotState.Empty && (MeatAvailable > 0 || VegetableAvailable > 0))
                return true;
        }

        return false;
    }

    // ---- 그릴 상태 동기화 ---------------------------------------------------

    [ClientRpc]
    private void RpcSlots(int[] kinds, double[] times) => ApplySlots(kinds, times);

    private void ApplySlots(int[] kinds, double[] times)
    {
        if (kinds == null || times == null)
            return;

        EnsureSlots(kinds.Length);
        Array.Copy(kinds, slotKind, Mathf.Min(kinds.Length, slotKind.Length));
        Array.Copy(times, slotPlacedAt, Mathf.Min(times.Length, slotPlacedAt.Length));
    }

    /// <summary>오프라인(네트워크 없이 씬만 연 경우)에서 상태 변경을 알린다.</summary>
    private void BroadcastSlotsLocal() => lastSignature = int.MinValue;

    private void EnsureSlots(int count = -1)
    {
        int n = count > 0 ? count : Mathf.Max(1, grillSlots);
        if (slotKind != null && slotKind.Length == n)
            return;

        slotKind = new int[n];
        slotPlacedAt = new double[n];
        for (int i = 0; i < n; i++)
            slotKind[i] = -1;
    }

    private void ClearSlots()
    {
        for (int i = 0; i < slotKind.Length; i++)
        {
            slotKind[i] = -1;
            slotPlacedAt[i] = 0d;
        }
    }

    private void ClearSlot(int index)
    {
        slotKind[index] = -1;
        slotPlacedAt[index] = 0d;
    }

    // ---- 페이즈 진행 -------------------------------------------------------

    [Server]
    private void ServerTick()
    {
        if (phaseEndsAt < 0d || NetworkTime.time < phaseEndsAt)
            return;

        switch ((CampPhase)phase)
        {
            case CampPhase.Dusk:
                phase = (int)CampPhase.Cooking;
                phaseEndsAt = -1d;
                break;

            case CampPhase.Feast:
                phaseEndsAt = -1d;
                ServerReturnToLobby();
                break;
        }
    }

    [Server]
    private void ServerEnterDusk()
    {
        if (phase != (int)CampPhase.Gathering)
            return;

        phase = (int)CampPhase.Dusk;
        phaseEndsAt = NetworkTime.time + duskSeconds;
    }

    [Server]
    private void ServerEnterFeast()
    {
        if (phase == (int)CampPhase.Feast)
            return;

        phase = (int)CampPhase.Feast;
        phaseEndsAt = NetworkTime.time + feastSeconds;
    }

    [Server]
    private void ServerReturnToLobby()
    {
        if (serverChangingScene || string.IsNullOrEmpty(lobbyScene) || NetworkManager.singleton == null)
            return;

        serverChangingScene = true;
        NetworkManager.singleton.ServerChangeScene(lobbyScene);
    }

    // ---- 내부 유틸 ---------------------------------------------------------

    private void BuildRegistry()
    {
        registry.Clear();
        foreach (CollectibleItem item in FindObjectsByType<CollectibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.ItemId >= 0)
                registry[item.ItemId] = item;
        }
    }

    /// <summary>씬에 실제로 놓인 재료 수로 목표치를 보정한다.</summary>
    [Server]
    private void CountPlacedIngredients()
    {
        int wood = 0, meat = 0, veg = 0;
        foreach (CollectibleItem item in registry.Values)
        {
            if (item == null)
                continue;
            switch (item.Kind)
            {
                case Ingredient.Firewood: wood++; break;
                case Ingredient.Meat: meat++; break;
                case Ingredient.Vegetable: veg++; break;
            }
        }

        if (wood > 0) firewoodNeeded = wood;
        if (meat > 0) meatNeeded = meat;
        if (veg > 0) vegetableNeeded = veg;

        if (wood + meat + veg == 0)
            Debug.LogWarning("[CampGame] 씬에 재료가 하나도 없습니다. " +
                             "'Tools > Stage > Build Stage 2 (Campground)'로 캠핑장을 다시 만드세요.");
    }

    private void Add(Ingredient kind, int amount)
    {
        switch (kind)
        {
            case Ingredient.Firewood: firewoodHave += amount; break;
            case Ingredient.Meat: meatHave += amount; break;
            case Ingredient.Vegetable: vegetableHave += amount; break;
        }
    }

    private void HideItem(int id)
    {
        if (registry.TryGetValue(id, out CollectibleItem item) && item != null)
            item.gameObject.SetActive(false);
    }

    /// <summary>네트워크 없이 씬을 열었을 때의 최소 동작(에디터에서 맵만 둘러볼 때).</summary>
    private void OfflineCollect(int id)
    {
        if (!registry.TryGetValue(id, out CollectibleItem item) || item == null)
            return;

        Add(item.Kind, 1);
        item.gameObject.SetActive(false);

        if (GatheringComplete && phase == (int)CampPhase.Gathering)
        {
            phase = (int)CampPhase.Dusk;
            phaseEndsAt = NetworkTime.time + duskSeconds;
        }
    }

    /// <summary>
    /// 상태가 바뀌었으면 이벤트를 울린다.
    ///
    /// SyncVar 훅을 쓰지 않는 이유: 훅이 호스트에서 불리는지가 Mirror 버전마다 다르다.
    /// 값을 한 덩어리로 해시해 비교하면 호스트·클라이언트·오프라인이 전부 같은 길로 간다.
    /// </summary>
    private void RaiseIfChanged()
    {
        int slotHash = 17;
        if (slotKind != null)
            foreach (int k in slotKind)
                slotHash = slotHash * 31 + k;

        int signature = HashCode.Combine(
            HashCode.Combine(phase, firewoodHave, meatHave, vegetableHave),
            HashCode.Combine(firewoodLoaded, meatUsed, vegetableUsed, cookedCount),
            burntCount, slotHash);

        if (signature == lastSignature)
            return;
        lastSignature = signature;

        if (phase != lastPhase)
        {
            lastPhase = phase;
            OnPhaseEntered?.Invoke((CampPhase)phase);
        }

        OnChanged?.Invoke();
    }

    // ---- 표시용 문구 -------------------------------------------------------

    /// <summary>HUD 상단에 띄울 현재 목표 한 줄.</summary>
    public string GoalLine()
    {
        switch (Phase)
        {
            case CampPhase.Gathering:
                return "해가 지기 전에 재료를 모아라";

            case CampPhase.Dusk:
                return "해가 넘어간다...";

            case CampPhase.Cooking:
                return FireLit
                    ? $"바베큐를 구워라   {cookedCount} / {CookTarget}"
                    : $"화로에 장작을 넣어라   {firewoodLoaded} / {firewoodNeeded}";

            case CampPhase.Feast:
                return "바베큐 완성! 잘 먹겠습니다";
        }
        return string.Empty;
    }

    /// <summary>재료 종류의 한국어 이름.</summary>
    public static string NameOf(Ingredient kind)
    {
        switch (kind)
        {
            case Ingredient.Firewood: return "장작";
            case Ingredient.Meat: return "고기";
            case Ingredient.Vegetable: return "야채";
        }
        return "재료";
    }
}
