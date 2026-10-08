using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全屏黑幕，用来遮盖「换房间」这种瞬间位移。
///
/// 它做的事很少但很关键：没有它，玩家会看到自己「啪」地一下被弹到另一个地方，
/// 那是很廉价的观感；有了它，同样的瞬移读起来像「推开门走了进去」。
/// 这是花钱最少、回报最高的一种打磨。
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("一张铺满屏幕的黑色 Image。初始透明度应当是 0，运行时会被接管")]
    [SerializeField] private Image overlay;

    [Header("时长（秒）")]
    [Tooltip("变黑要多久。太慢会让玩家等，0.2 秒左右最舒服")]
    [SerializeField] private float fadeOutDuration = 0.22f;

    [Tooltip("变回来要多久。一般比变黑稍微慢一点点，收尾更柔和")]
    [SerializeField] private float fadeInDuration = 0.28f;

    [Tooltip("全黑之后额外停多久，给新房间的实例化留余量")]
    [SerializeField] private float holdDuration = 0.05f;

    /// <summary>现在是不是全黑</summary>
    public bool IsCovered => overlay != null && overlay.color.a >= 0.99f;

    private void Awake()
    {
        // 进场必须是透明的，否则玩家开局什么都看不见
        SetAlpha(0f);
    }

    /// <summary>渐变到全黑</summary>
    public IEnumerator FadeOut()
    {
        yield return FadeRoutine(1f, fadeOutDuration);

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);
    }

    /// <summary>从当前透明度渐变回透明</summary>
    public IEnumerator FadeIn()
    {
        yield return FadeRoutine(0f, fadeInDuration);
    }

    /// <summary>不走动画，立刻全黑 / 立刻透明</summary>
    public void SetBlack(bool black) => SetAlpha(black ? 1f : 0f);

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (overlay == null) yield break;

        float startAlpha = overlay.color.a;

        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 用 unscaledDeltaTime：就算将来做了「暂停」把 timeScale 设成 0，
            // 黑幕也必须能正常淡出，否则会卡在半黑状态再也回不来
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (overlay == null) return;

        Color c = overlay.color;
        c.a = Mathf.Clamp01(alpha);
        overlay.color = c;

        // 完全透明时把物体关掉。一张 alpha = 0 的 Image 仍然会挡住射线，
        // 将来做背包 / 暂停菜单时会莫名其妙点不动，现在顺手处理掉。
        //
        // ★ 但如果 ScreenFader 组件就挂在这张图上，就绝不能关它 ——
        //   关掉自己的 gameObject 会把正在跑的协程一起停掉，
        //   表现是「黑幕淡出到一半卡住，再也回不来」。
        //   正常用法是把组件挂在 Canvas 上，Overlay 指向一张独立的子图。
        if (overlay.gameObject == gameObject) return;

        bool shouldBeActive = c.a > 0.001f;
        if (overlay.gameObject.activeSelf != shouldBeActive)
            overlay.gameObject.SetActive(shouldBeActive);
    }
}
