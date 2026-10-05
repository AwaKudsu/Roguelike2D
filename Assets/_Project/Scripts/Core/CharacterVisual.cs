using UnityEngine;

/// <summary>
/// 角色外观：目前只干一件事 —— **让贴图朝着角色实际面对的方向**。
///
/// 现在角色是个正方形，翻不翻看不出来；等换成真正的贴图，
/// 如果不翻，就会出现「人往左走、刀往左砍，但人一直脸朝右」这种诡异画面。
/// 所以这个组件趁现在就装上，它不花什么成本，但省掉了以后的一次排查。
///
/// 它不管动画、不管颜色闪烁 —— 那两件事分别归 Animator 和 HitReaction。
/// </summary>
[DisallowMultipleComponent]
public class CharacterVisual : MonoBehaviour
{
    [Tooltip("留空会自动在本物体和所有子物体里找第一个 SpriteRenderer")]
    [SerializeField] private SpriteRenderer sprite;

    [Tooltip("勾上后，翻转时打印日志，用来确认朝向真的被改了")]
    [SerializeField] private bool debugLog = false;

    /// <summary>当前朝向：1 = 右，-1 = 左</summary>
    public int Facing { get; private set; } = 1;

    /// <summary>外观渲染器，给将来的换装 / 换皮肤用</summary>
    public SpriteRenderer Sprite => sprite;

    private void Awake()
    {
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();

        if (sprite == null)
        {
            Debug.LogWarning(
                "[CharacterVisual] 本物体和子物体里都没有 SpriteRenderer，朝向翻转不会生效。" +
                "如果是故意不显示外观（比如纯逻辑测试），可以忽略这条。", this);
            return;
        }

        Apply();
    }

    /// <summary>设置朝向。传 0 或正数当朝右，负数当朝左</summary>
    public void SetFacing(int facing)
    {
        facing = facing < 0 ? -1 : 1;
        if (facing == Facing) return;

        Facing = facing;
        Apply();
    }

    /// <summary>按一个水平方向值决定朝向。传 0 不会改变现有朝向</summary>
    public void FaceTowards(float horizontal)
    {
        if (Mathf.Abs(horizontal) < 0.01f) return;
        SetFacing(horizontal > 0f ? 1 : -1);
    }

    /// <summary>换外观贴图。将来做「换皮肤」「变身」时用得上</summary>
    public void SetSprite(Sprite newSprite)
    {
        if (sprite != null) sprite.sprite = newSprite;
    }

    private void Apply()
    {
        if (sprite == null) return;

        // flipX 只翻转这一个 SpriteRenderer。
        // 以后如果角色身上挂了独立的武器 / 披风贴图，它们的朝向要单独处理 ——
        // 简单做法是给它们各自也挂一个 CharacterVisual，或者统一放到一个会整体缩放的子物体下。
        sprite.flipX = Facing < 0;

        if (debugLog) Debug.Log($"[CharacterVisual]「{name}」朝向 → {(Facing < 0 ? "左" : "右")}", this);
    }
}
