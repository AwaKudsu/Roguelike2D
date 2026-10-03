using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一局游戏的生命周期管理。
///
/// 现在只做一件事：玩家死亡 → 停顿一下 → 重开场景。
/// 后面肉鸽的「随机地牢生成」「通关强化」「局内进度重置」都会挂在这里。
/// </summary>
public class RunManager : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("留空则自动找场景里 Tag 为 Player 的对象")]
    [SerializeField] private Health playerHealth;

    [Header("死亡流程")]
    [Tooltip("死亡后等待多久重开，留一点时间让玩家看清自己是怎么死的")]
    [SerializeField] private float restartDelay = 1.5f;

    [Tooltip("关掉后只打日志不重开，方便调试")]
    [SerializeField] private bool autoRestart = true;

    /// <summary>玩家是否已死亡。将来 UI 和肉鸽结算会读它</summary>
    public bool IsPlayerDead { get; private set; }

    private void Awake()
    {
        if (playerHealth == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerHealth = go.GetComponent<Health>();
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
    }

    private void OnPlayerDied()
    {
        if (IsPlayerDead) return;   // 玩家可能同时被多个来源打死，只处理一次
        IsPlayerDead = true;

        Debug.Log("[RunManager] 玩家死亡");

        if (autoRestart) StartCoroutine(RestartRoutine());
    }

    private IEnumerator RestartRoutine()
    {
        yield return new WaitForSeconds(restartDelay);

        // 重载当前场景：位置、血量、敌人全部回到初始状态。
        // 这是最粗暴也最可靠的重开方式 —— 等肉鸽系统上线后再换成「只重置局内状态」。
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}