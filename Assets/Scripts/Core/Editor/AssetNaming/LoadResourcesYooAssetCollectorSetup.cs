using System.Linq;
using Core.Editor.MvcBind;
using UnityEditor;
using YooAsset.Editor;

namespace Core.Editor.AssetNaming
{
    /// <summary>
    /// 确保 LoadResources 下公共与 Demo 资源目录纳入 YooAsset 采集，地址规则与 UI 一致（全路径）。
    /// </summary>
    public static class LoadResourcesYooAssetCollectorSetup
    {
        private const string SettingPath = "Assets/Settings/AssetBundleCollectorSetting.asset";
        private const string PackageName = "DefaultPackage";

        private static readonly CollectorEntry[] Entries =
        {
            new CollectorEntry("Demos", "Assets/LoadResources/Demos", "Demo 可加载资源"),
            new CollectorEntry("Art", "Assets/LoadResources/Art", "公共美术"),
            new CollectorEntry("Audio", "Assets/LoadResources/Audio", "音频"),
            new CollectorEntry("VFX", "Assets/LoadResources/VFX", "特效"),
            new CollectorEntry("Scenes", "Assets/LoadResources/Scenes", "可加载场景"),
            new CollectorEntry("Config", "Assets/LoadResources/Config", "配置"),
            new CollectorEntry("Fonts", "Assets/LoadResources/Fonts", "字体")
        };

        [MenuItem("Tools/SleepyDemos/把 LoadResources 下的目录加入 YooAsset 打包采集配置")]
        public static void EnsureFromMenu()
        {
            EnsureCollectors();
            UnityEngine.Debug.Log(
                "[AssetNaming] 已写入/校正 YooAsset 采集配置：Demos、Art、Audio、VFX、Scenes、Config、Fonts（地址为 LoadResources 全路径）。配置已保存到 BundleCollectorSetting。");
        }

        public static void EnsureCollectors()
        {
            var setting = AssetDatabase.LoadAssetAtPath<BundleCollectorSetting>(SettingPath);
            if (setting == null)
            {
                return;
            }

            var package = setting.Packages.FirstOrDefault(item => item.PackageName == PackageName);
            if (package == null)
            {
                return;
            }

            foreach (var entry in Entries)
            {
                if (!AssetDatabase.IsValidFolder(entry.CollectPath))
                {
                    continue;
                }

                EnsureGroupCollector(package, entry);
            }

            EditorUtility.SetDirty(setting);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureGroupCollector(BundleCollectorPackage package, CollectorEntry entry)
        {
            var group = package.Groups.FirstOrDefault(item => item.GroupName == entry.GroupName);
            if (group == null)
            {
                group = new BundleCollectorGroup
                {
                    GroupName = entry.GroupName,
                    GroupDesc = entry.GroupDesc
                };
                package.Groups.Add(group);
            }

            var collector = group.Collectors.FirstOrDefault(item => item.CollectPath == entry.CollectPath);
            if (collector == null)
            {
                group.Collectors.Add(new BundleCollector
                {
                    CollectPath = entry.CollectPath,
                    CollectorGUID = AssetDatabase.AssetPathToGUID(entry.CollectPath),
                    CollectorType = ECollectorType.MainAssetCollector,
                    AddressRuleName = YooAssetFullPathAddressRule.RuleName,
                    PackRuleName = "PackDirectory",
                    FilterRuleName = "CollectAll"
                });
            }
            else
            {
                collector.CollectorGUID = AssetDatabase.AssetPathToGUID(entry.CollectPath);
                collector.AddressRuleName = YooAssetFullPathAddressRule.RuleName;
                collector.PackRuleName = "PackDirectory";
                collector.FilterRuleName = "CollectAll";
            }
        }

        private readonly struct CollectorEntry
        {
            public readonly string GroupName;
            public readonly string CollectPath;
            public readonly string GroupDesc;

            public CollectorEntry(string groupName, string collectPath, string groupDesc)
            {
                GroupName = groupName;
                CollectPath = collectPath;
                GroupDesc = groupDesc;
            }
        }
    }
}
