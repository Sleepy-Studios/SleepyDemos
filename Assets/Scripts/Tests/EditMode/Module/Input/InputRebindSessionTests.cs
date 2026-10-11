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
                using var first = new InputRebindSession(asset);
                first.SetBinding("Gameplay/Jump", 0, "<Keyboard>/j");
                Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
                Assert.Throws<ArgumentException>(() => first.SetBinding("Gameplay/Jump", 0, "<Keyboard>/e"));
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
