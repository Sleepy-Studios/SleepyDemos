using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino
{
    /// 赌场内容与输入的可编辑配置；运行局使用副本，不能覆盖资产中的默认值。
    [CreateAssetMenu(menuName = "SleepyDemos/JinxCasino/冒险设置")]
    public sealed class JinxCasinoGameSettings : ScriptableObject
    {
        [SerializeField] private CasinoAdventureConfig adventure = new CasinoAdventureConfig();
        [SerializeField, Range(0.02f, 0.5f)] private float lookSensitivity = 0.12f;
        [SerializeField, Range(1f, 8f)] private float movementSpeed = 3.5f;

        /// 鼠标像素及触控720p参考像素的视角系数。
        public float LookSensitivity => lookSensitivity;
        /// 每秒移动米数，两端使用同一上限。
        public float MovementSpeed => movementSpeed;
        /// 标准冒险默认配置的深拷贝。
        public CasinoAdventureConfig CreateConfig() => JsonUtility.FromJson<CasinoAdventureConfig>(JsonUtility.ToJson(adventure));
    }
}
