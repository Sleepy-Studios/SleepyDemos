using System;
using UnityEngine;

namespace Hotfix.BlockPorters
{
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "SleepyDemos/BlockPorters/关卡集")]
    public sealed class BlockPortersLevelCatalog : ScriptableObject
    {
        [SerializeField] private BlockPortersLevel[] levels = Array.Empty<BlockPortersLevel>();
        public BlockPortersLevel[] Levels => levels;

        /// <summary>更新顺序；每项必须是可加载且人数守恒的关卡。</summary>
        /// <param name="definitions">至少一个关卡，数组复制后保存。</param>
        public void Configure(BlockPortersLevel[] definitions)
        {
            if (definitions == null || definitions.Length == 0) throw new ArgumentException("关卡集不能为空。");
            foreach (var level in definitions) { if (level == null) throw new ArgumentException("关卡集含空引用。"); level.CreateData(); }
            levels = (BlockPortersLevel[])definitions.Clone();
        }
    }
}
