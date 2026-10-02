using UnityEngine;

namespace Hotfix.JinxCasino.Interaction
{
    /// 区域内容和安全出生点的装配，不自行持有冒险进度。
    public sealed class JinxCasinoWorldArea : MonoBehaviour
    {
        [SerializeField, Range(0, 3)] private int index;
        [SerializeField] private Transform safeSpawn;
        [SerializeField] private GameObject contents;
        [SerializeField] private GameObject lockedGate;

        /// 区域稳定索引。
        public int Index => index;
        /// 本地玩家可以安全传送的位置。
        public Vector3 SafePosition => safeSpawn != null ? safeSpawn.position : transform.position;

        /// <summary>装配区域；安全点始终存在，内容和门按当前进度显隐。</summary>
        /// <param name="area">稳定区域索引。</param>
        /// <param name="spawn">无障碍的安全位置。</param>
        /// <param name="contentRoot">该区域的机台、装饰和任务对象根。</param>
        /// <param name="gate">未解锁区域入口的阻挡物。</param>
        public void Configure(int area, Transform spawn, GameObject contentRoot, GameObject gate)
        {
            index = area; safeSpawn = spawn; contents = contentRoot; lockedGate = gate;
        }

        /// <summary>根据冒险阶段更新区域可用性。</summary>
        /// <param name="unlocked">true开放内容与通路；false保留区域结构并封锁入口。</param>
        public void SetUnlocked(bool unlocked)
        {
            if (contents != null) contents.SetActive(unlocked);
            if (lockedGate != null) lockedGate.SetActive(!unlocked);
        }
    }
}
