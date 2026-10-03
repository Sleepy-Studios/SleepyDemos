using System;
using System.Reflection;
using Core.Runtime;
using NUnit.Framework;

namespace Tests.Module
{
    /// 页面发现和缓存构造的公开契约，不需要业务或测试手写注册表。
    public sealed class UITypeReflectionTests
    {
        private sealed class AutoPage : View { }
        [Mvc("DiscoveryAlias")]
        private sealed class AliasedPage : View { }
        private abstract class AbstractPage : View { }
        private sealed class OpenPage<T> : View { }
        private sealed class ListItem : ItemView { }
        [Mvc("DiscoveryCollision")]
        private sealed class FirstPage : View { }
        [Mvc("DiscoveryCollision")]
        private sealed class SecondPage : View { }

        private sealed class CountingAssembly : Assembly
        {
            private readonly Type[] types;
            public int Scans { get; private set; }
            public CountingAssembly(params Type[] types) => this.types = types;
            public override Type[] GetTypes() { Scans++; return types; }
        }

        [TearDown]
        public void Cleanup() => UITypeReflection.Init();

        [Test]
        public void DiscoveryUsesInheritanceAndMvcAliasButExcludesNonPages()
        {
            UITypeReflection.Init(new CountingAssembly(typeof(AutoPage), typeof(AliasedPage),
                typeof(AbstractPage), typeof(OpenPage<>), typeof(ListItem), typeof(View)));
            Assert.That(UITypeReflection.Get(nameof(AutoPage)), Is.EqualTo(typeof(AutoPage)));
            Assert.That(UITypeReflection.Get(nameof(AliasedPage)), Is.EqualTo(typeof(AliasedPage)));
            Assert.That(UITypeReflection.Get("DiscoveryAlias"), Is.EqualTo(typeof(AliasedPage)));
            Assert.That(UITypeReflection.Get(nameof(AbstractPage)), Is.Null);
            Assert.That(UITypeReflection.Get(typeof(OpenPage<>).Name), Is.Null);
            Assert.That(UITypeReflection.Get(nameof(ListItem)), Is.Null);
            Assert.That(UITypeReflection.Get(nameof(View)), Is.Null);
        }

        [Test]
        public void RepeatedAssemblyScanRunsOnceAndUnknownLookupNeverDiscoversMoreTypes()
        {
            var assembly = new CountingAssembly(typeof(AutoPage));
            UITypeReflection.Init(assembly, assembly);
            UITypeReflection.Scan(assembly);
            Assert.That(assembly.Scans, Is.EqualTo(1));
            Assert.That(UITypeReflection.Get(nameof(FirstPage)), Is.Null);
            Assert.That(UITypeReflection.Get(null), Is.Null);
            Assert.That(assembly.Scans, Is.EqualTo(1));
            UITypeReflection.Init();
            Assert.That(UITypeReflection.Get(nameof(AutoPage)), Is.Null,
                "缓存为空时也不能兜底扫描已加载的测试程序集。");
            UITypeReflection.Scan(assembly);
            Assert.That(assembly.Scans, Is.EqualTo(2), "新一轮启动需要重新发现程序集。");
        }

        [Test]
        public void SharedAliasKeepsFirstDiscoveredTypeAndBothClassNames()
        {
            UITypeReflection.Init(new CountingAssembly(typeof(FirstPage), typeof(SecondPage)));
            Assert.That(UITypeReflection.Get("DiscoveryCollision"), Is.EqualTo(typeof(FirstPage)));
            Assert.That(UITypeReflection.Get(nameof(SecondPage)), Is.EqualTo(typeof(SecondPage)));
        }

        [Test]
        public void TypedCacheCreatesWithoutRegistrationReusesAndRecreatesRemovedInstance()
        {
            UITypeReflection.Init();
            var cache = new UICache();
            var first = cache.GetOrCreateView<AutoPage>();
            Assert.That(first, Is.Not.Null);
            Assert.That(cache.GetOrCreateView(typeof(AutoPage), out bool created), Is.SameAs(first));
            Assert.That(created, Is.False);
            cache.Remove(first);
            Assert.That(cache.GetOrCreateView(typeof(AutoPage), out created), Is.Not.SameAs(first));
            Assert.That(created, Is.True);
            Assert.That(UITypeReflection.Get(nameof(AutoPage)), Is.Null,
                "强类型创建不应隐式改变字符串发现范围。");
        }
    }
}
