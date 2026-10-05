using System.Collections.Generic;
using Core.Runtime;
using NUnit.Framework;

namespace Tests.Module
{
    public sealed class GlobalDataTests
    {
        private sealed class SetValue : IAction
        {
            public int Value;

            public bool Result { get; internal set; }
        }

        private sealed class Data : IData
        {
            public readonly Handler Handler = new();

            public int Value;

            public List<IHandler> Handlers => new()
            {
                Handler
            };

            public void ClearData() => Value = 0;
        }

        private sealed class Handler : HandlerBase<SetValue, Data>
        {
            protected override void Reduce(SetValue action)
            {
                State.Value = action.Value;
                action.Result = true;
                ApplyState();
            }
        }

        [TearDown]
        public void Cleanup() => GlobalData.Remove<Data>();
        [Test]
        public void DispatchPublishesRegisteredStateAndImmediateSubscription()
        {
            var data = GlobalData.Add<Data>();
            int calls = 0, value = -1;
            GlobalData.Subscribe<Data>(state =>
            {
                calls++;
                value = state.Value;
            });
            Assert.That(calls, Is.EqualTo(1));
            var request = new SetValue
            {
                Value = 7
            };
            bool resultDuringRefresh = false;
            GlobalData.Subscribe<Data>(_ => resultDuringRefresh = request.Result, false);
            GlobalData.Dispatch(request);
            Assert.That(request.Result, Is.True);
            Assert.That(resultDuringRefresh, Is.True, "同步结果在订阅回调执行前可读。");
            Assert.That(value, Is.EqualTo(7));
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(GlobalData.Add<Data>(), Is.SameAs(data));
        }

        [Test]
        public void ClearKeepsRegistrationAndAllowsNextCommand()
        {
            var data = GlobalData.Add<Data>();
            GlobalData.Dispatch(new SetValue { Value = 9 });
            int value = -1;
            GlobalData.Subscribe<Data>(state => value = state.Value, false);
            GlobalData.ClearData();
            Assert.That(GlobalData.Get<Data>(), Is.SameAs(data));
            Assert.That(value, Is.Zero);
            GlobalData.Dispatch(new SetValue { Value = 4 });
            Assert.That(value, Is.EqualTo(4));
        }

        [Test]
        public void RemovedHandlerCannotPublishIntoReplacementData()
        {
            var old = GlobalData.Add<Data>();
            GlobalData.Remove<Data>();
            var current = GlobalData.Add<Data>();
            int calls = 0;
            GlobalData.Subscribe<Data>(_ => calls++, false);
            old.Handler.ReduceAny(new SetValue { Value = 99 });
            Assert.That(GlobalData.Get<Data>(), Is.SameAs(current));
            Assert.That(current.Value, Is.Zero);
            Assert.That(calls, Is.Zero);
            GlobalData.Dispatch(new SetValue { Value = 2 });
            Assert.That(current.Value, Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
