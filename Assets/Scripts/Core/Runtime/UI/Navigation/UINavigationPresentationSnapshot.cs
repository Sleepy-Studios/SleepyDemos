using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    internal sealed class UINavigationPresentationSnapshot
    {
        private readonly List<ViewPresentationState> views;
        private readonly Transform mask;
        private readonly Transform maskParent;
        private readonly int maskSiblingIndex;
        private readonly Vector3 maskLocalScale;
        private readonly Vector3 maskLocalPosition;
        private readonly Button maskButton;
        private readonly bool maskInteractable;

        internal UINavigationPresentationSnapshot(
            IReadOnlyList<View> sourceViews,
            Transform mask,
            Button maskButton,
            string currentUIName,
            string lastCloseName)
        {
            views = new List<ViewPresentationState>(sourceViews.Count);
            for (int i = 0; i < sourceViews.Count; i++)
            {
                var view = sourceViews[i];
                if (view?.transform != null && view.gameObject != null)
                {
                    views.Add(new ViewPresentationState(view));
                }
            }

            this.mask = mask;
            this.maskButton = maskButton;
            CurrentUIName = currentUIName;
            LastCloseName = lastCloseName;
            if (mask != null)
            {
                maskParent = mask.parent;
                maskSiblingIndex = mask.GetSiblingIndex();
                maskLocalScale = mask.localScale;
                maskLocalPosition = mask.localPosition;
            }

            if (maskButton != null)
            {
                maskInteractable = maskButton.interactable;
            }
        }

        internal string CurrentUIName { get; }
        internal string LastCloseName { get; }

        internal void Restore(View excludedView, Action<Exception> reportFailure)
        {
            var ordered = new List<ViewPresentationState>(views);
            ordered.Sort((left, right) => left.SiblingIndex.CompareTo(right.SiblingIndex));
            foreach (var state in ordered)
            {
                if (ReferenceEquals(state.View, excludedView))
                {
                    continue;
                }

                try
                {
                    state.Restore();
                }
                catch (Exception exception)
                {
                    reportFailure?.Invoke(exception);
                }
            }

            try
            {
                if (mask != null)
                {
                    mask.SetParent(maskParent, false);
                    mask.SetSiblingIndex(maskSiblingIndex);
                    mask.localScale = maskLocalScale;
                    mask.localPosition = maskLocalPosition;
                }

                if (maskButton != null)
                {
                    maskButton.interactable = maskInteractable;
                }
            }
            catch (Exception exception)
            {
                reportFailure?.Invoke(exception);
            }
        }

        private sealed class ViewPresentationState
        {
            internal ViewPresentationState(View view)
            {
                View = view;
                Parent = view.transform.parent;
                SiblingIndex = view.transform.GetSiblingIndex();
                Active = view.gameObject.activeSelf;
                State = view.State;
                Reference = view.Reference;
            }

            internal View View { get; }
            internal Transform Parent { get; }
            internal int SiblingIndex { get; }
            internal bool Active { get; }
            internal ViewState State { get; }
            internal int Reference { get; }

            internal void Restore()
            {
                if (View?.transform == null || View.gameObject == null)
                {
                    return;
                }

                if (View.State == ViewState.Destroying || View.State == ViewState.Destroyed)
                {
                    return;
                }

                View.transform.SetParent(Parent, false);
                View.transform.SetSiblingIndex(SiblingIndex);
                View.Reference = Reference;
                View.RestoreAfterNavigationFailure(State, Active);
            }
        }
    }
}
