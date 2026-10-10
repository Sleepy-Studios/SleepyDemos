using System;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 原作场景的保存数据；网格、印刷材质和折起触发点随赛道一起加载。
    public sealed class KagitKartSceneryAsset : ScriptableObject
    {
        [SerializeField] internal string trackId;
        [SerializeField] internal float length;
        [SerializeField] internal Color skyTop;
        [SerializeField] internal Color skyBottom;
        [SerializeField] internal Vector3 sunDirection;
        [SerializeField] internal Color sunColor;
        [SerializeField] internal float sunIntensity;
        [SerializeField] internal Material skyMaterial;
        [SerializeField] internal KagitKartSceneryBatch[] batches;
        [SerializeField] internal KagitKartPopupGroup[] popups;
        [SerializeField] internal KagitKartSceneryRamp[] ramps;
        [SerializeField] internal TextAsset dynamicsData;

        public string TrackId => trackId;
        public Color SkyTop => skyTop;
        public Color SkyBottom => skyBottom;
        public Vector3 SunDirection => sunDirection;
        public Color SunColor => sunColor;
        public float SunIntensity => sunIntensity;
        public Material SkyMaterial => skyMaterial;
    }

    [Serializable]
    internal sealed class KagitKartSceneryBatch
    {
        public string sourceName;
        public Mesh mesh;
        public Material[] materials;
        public Matrix4x4 parent;
        public Matrix4x4[] poses;
        public Vector4[] colors;
        public bool castShadow;
        public bool receiveShadow;
    }

    [Serializable]
    internal sealed class KagitKartPopupGroup
    {
        public bool volume;
        public bool flutter;
        public int[] batches;
        public KagitKartPopupItem[] items;
    }

    [Serializable]
    internal struct KagitKartPopupItem
    {
        public Vector3 position;
        public Vector3 scale;
        public float yaw;
        public float distance;
        public float trigger;
        public bool alwaysUp;
    }

    [Serializable]
    internal struct KagitKartSceneryRamp
    {
        public float distance;
        public float theta;
    }
}
