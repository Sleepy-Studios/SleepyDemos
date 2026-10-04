using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 岛屿的航行与鱼池标记；坐标直接取保存场景中的岛屿根节点。
    public sealed class HowToFishIsland : MonoBehaviour
    {
        [SerializeField] private int index;
        [SerializeField] private string displayName;
        [SerializeField] private float radius = 36;

        public int Index => index;
        public string DisplayName => displayName;
        public float Radius => radius;
        public Vector3 Position => transform.position;

        /// <summary>计算到岸线的水平距离；在岛内为零。</summary>
        /// <param name="position">玩家或船舶的世界坐标。</param>
        public float DistanceToShore(Vector3 position) => Mathf.Max(0,
            Vector2.Distance(new Vector2(position.x, position.z), new Vector2(transform.position.x, transform.position.z)) - radius);
    }
}
