using System;
using Core.Runtime.Inputs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class InputRebindSessionTests
    {
        [Test]
        public void CandidatesPreserveSourceAndRejectConflictsAndFixedMenuBindings()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplay = asset.AddActionMap("Gameplay");
            var jump = gameplay.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            var interact = gameplay.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            var menu = asset.AddActionMap("Menu");
            menu.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            try
            {
                string sourceId = InputActionSession.GetSourceId(asset);
                var host = new InputSettingsCatalog.Entry("Input.Host", asset);
                var actionLabels = new[] { new InputSettingsCatalog.ActionLabel(jump, "Input.Jump") };
                host.ConfigureActions(actionLabels);
                actionLabels[0] = new InputSettingsCatalog.ActionLabel(interact, "Input.Interact");
                Assert.That(host.Actions[0].ActionId, Is.EqualTo(jump.id.ToString()));
                Assert.Throws<ArgumentException>(() => host.ConfigureActions(new[] { host.Actions[0], host.Actions[0] }));
                Assert.That(asset.FindAction(host.Actions[0].ActionId), Is.SameAs(jump));
                using var first = new InputRebindSession(asset);
                Assert.That(first.CanRebind("Gameplay/Jump", 0, out _), Is.True);
                Assert.That(first.CanRebind("Menu/Submit", 0, out string reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
                Assert.That(first.CanRebind("Gameplay/Jump", 20, out _), Is.False);
                first.SetBinding("Gameplay/Jump", 0, "<Keyboard>/j");
                Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
                Assert.Throws<ArgumentException>(() => first.SetBinding("Gameplay/Jump", 0, "<Keyboard>/e"));
                Assert.That(first.ConflictingActionId, Is.EqualTo(interact.id.ToString()));
                Assert.That(first.Asset.FindAction("Gameplay/Jump").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/j"));
                Assert.Throws<ArgumentException>(() => first.SetBinding("Menu/Submit", 0, "<Keyboard>/q"));
                using var restored = new InputRebindSession(asset, first.ExportOverrides());
                Assert.That(restored.SourceId, Is.EqualTo(sourceId));
                Assert.That(restored.Asset.FindAction("Gameplay/Jump").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/j"));
                Assert.That(interact.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/e"));
                first.ResetAction("Gameplay/Jump");
                Assert.That(first.Asset.FindAction("Gameplay/Jump").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
                asset.name = "Renamed";
                Assert.That(InputActionSession.GetSourceId(asset), Is.EqualTo(sourceId));
                Assert.Throws<ArgumentException>(() => new InputRebindSession(asset,
                    "{\"bindings\":[{\"id\":\"" + Guid.NewGuid() + "\",\"path\":\"<Keyboard>/k\"}]}"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void DifferentShortcutsCanShareModifierWithoutConflict()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Gameplay");
            map.AddAction("First", InputActionType.Button).AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", "<Keyboard>/leftCtrl").With("Button", "<Keyboard>/r");
            map.AddAction("Second", InputActionType.Button).AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", "<Keyboard>/leftCtrl").With("Button", "<Keyboard>/q");
            try
            {
                using var candidate = new InputRebindSession(asset);
                candidate.SetBinding("Gameplay/First", 2, "<Keyboard>/t");
                Assert.DoesNotThrow(() => candidate.ExportOverrides());
                Assert.Throws<ArgumentException>(() => candidate.SetBinding("Gameplay/First", 2, "<Keyboard>/q"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
