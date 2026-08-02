using System.Linq;
using UnityEditor;
using YooAsset.Editor;

namespace Core.Editor.MvcBind
{
    public static class YooAssetDemoCollectorSetup
    {
        public static void EnsureDemoCollector()
        {
            const string settingPath = "Assets/Settings/AssetBundleCollectorSetting.asset";
            const string packageName = "DefaultPackage";
            const string groupName = "UI";
            const string collectPath = MvcBindPathUtility.DefaultUiPrefabRoot;

            var setting = AssetDatabase.LoadAssetAtPath<BundleCollectorSetting>(settingPath);
            if (setting == null)
            {
                return;
            }

            var package = setting.Packages.FirstOrDefault(item => item.PackageName == packageName);
            if (package == null)
            {
                package = new BundleCollectorPackage
                {
                    PackageName = packageName,
                    PackageDesc = "默认",
                    EnableAddressable = true,
                    SupportExtensionless = true,
                    IncludeAssetGUID = true,
                    AutoCollectShaders = true
                };
                setting.Packages.Add(package);
            }

            var group = package.Groups.FirstOrDefault(item => item.GroupName == groupName);
            if (group == null)
            {
                group = new BundleCollectorGroup
                {
                    GroupName = groupName,
                    GroupDesc = "UI assets"
                };
                package.Groups.Add(group);
            }

            var collector = group.Collectors.FirstOrDefault(item => item.CollectPath == collectPath);
            if (collector == null)
            {
                group.Collectors.Add(new BundleCollector
                {
                    CollectPath = collectPath,
                    CollectorGUID = AssetDatabase.AssetPathToGUID(collectPath),
                    CollectorType = ECollectorType.MainAssetCollector,
                    AddressRuleName = YooAssetFullPathAddressRule.RuleName,
                    PackRuleName = "PackDirectory",
                    FilterRuleName = "CollectPrefab"
                });
            }
            else
            {
                collector.CollectorGUID = AssetDatabase.AssetPathToGUID(collectPath);
                collector.AddressRuleName = YooAssetFullPathAddressRule.RuleName;
                collector.PackRuleName = "PackDirectory";
                collector.FilterRuleName = "CollectPrefab";
            }

            EditorUtility.SetDirty(setting);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [DisplayName("定位地址: Assets相对路径")]
    public sealed class YooAssetFullPathAddressRule : IAddressRule
    {
        public const string RuleName = nameof(YooAssetFullPathAddressRule);

        string IAddressRule.GetAssetAddress(AddressRuleData data)
        {
            return MvcBindPathUtility.ToRuntimeAddress(data.AssetPath);
        }
    }
}
