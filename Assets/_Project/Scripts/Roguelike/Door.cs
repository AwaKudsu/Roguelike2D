using UnityEngine;

/// <summary>
/// 房间尽头的门。
///
/// 它有两种状态，而且这两种状态是靠**碰撞体本身**区分的，不是靠一张贴图：
///   锁着 —— Collider2D 是实心的，它就是一面墙，玩家物理上过不去
///   开了 —— Collider2D 变成触发器，玩家能穿过去，穿过去的瞬间触发换房间
///
/// 用「实体墙」而不是「脚本拦住玩家」有个好处：玩家推不过去是物理事实，
/// 没有任何绕过的可能，也不需要每帧去检查玩家是不是想偷跑。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("门属于哪个房间。留空则往上自动找")]
    [SerializeField] private Room room;

    [Tooltip("门的显示。会随状态换颜色。留空则自动找")]
    [SerializeField] private SpriteRenderer visual;

    [Header("配色")]
    [SerializeField] private Color lockedColor = new Color(0.35f, 0.35f, 0.42f, 1f);
    [SerializeField] private Color unlockedColor = new Color(0.30f, 0.90f, 1.00f, 1f);

    [Header("规则")]
    [Tooltip("玩家碰到门就立刻换房间")]
    [SerializeField] private bool advanceOnEnter = true;

    /// <summary>门是否已解锁（= 敌人清光了）</summary>
    public bool IsUnlocked { get; private set; }

    private Collider2D _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();

        if (room == null) room = GetComponentInParent<Room>();
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>(true);

        // 出生时一律是锁着的。哪怕预制体里把 Is Trigger 勾了，这里也会掰回来，
        // 免得出现「门看起来是灰的但其实能穿过去」
        IsUnlocked = false;
        ApplyState();
    }

    /// <summary>敌人清光后由 Room 调用</summary>
    public void Unlock()
    {
        if (IsUnlocked) return;

        IsUnlocked = true;
        ApplyState();
    }

    private void ApplyState()
    {
        if (_collider != null)
        {
            // 锁着 = 实心墙；开了 = 触发器。
            // 注意 Collider2D 一旦有 Rigidbody2D 的物体撞上来，isTrigger 的切换会立刻生效，
            // 但「已经站在门里的玩家」不会补发 OnTriggerEnter —— 所以下面还写了 Stay
            _collider.isTrigger = IsUnlocked;
        }

        if (visual != null)
            visual.color = IsUnlocked ? unlockedColor : lockedColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryAdvance(other);
    }

    /// <summary>
    /// 兜底：如果玩家在门解锁的那一瞬间正好站在门里，Enter 是不会补发的，
    /// 只有 Stay 能救回来。RoomManager.Advance 自己有防重入，所以这里多喊几次没关系。
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        TryAdvance(other);
    }

    private void TryAdvance(Collider2D other)
    {
        if (!advanceOnEnter) return;
        if (!IsUnlocked) return;
        if (other == null) return;

        // 用 Tag 而不是 Layer：门只对玩家有反应，
        // 敌人、投射物、掉落在门口的装备都不该把玩家带走
        if (!other.CompareTag("Player")) return;

        if (room != null) room.RequestAdvance();
    }

    private void OnValidate()
    {
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>(true);
        if (room == null) room = GetComponentInParent<Room>();
    }
}
