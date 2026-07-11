using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Core.Runtime
{
    internal sealed class UIStack
    {
        private readonly List<View> widgets = new List<View>();
        private readonly ReadOnlyCollection<View> readOnlyWidgets;
        private readonly StackData currentStack = new StackData();

        internal UIStack()
        {
            readOnlyWidgets = widgets.AsReadOnly();
        }

        internal View CurrentPage => GetTop(currentStack.Pages);
        internal View TopModal => GetTop(currentStack.Modals);
        internal IReadOnlyList<View> Pages => currentStack.ReadOnlyPages;
        internal IReadOnlyList<View> Modals => currentStack.ReadOnlyModals;
        internal IReadOnlyList<View> Widgets => readOnlyWidgets;

        internal int TotalCount => currentStack.Count + widgets.Count;
        internal View StackTopView => TopModal ?? CurrentPage;

        internal void CommitShow(View view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            RemoveFromCollections(view);
            GetCollection(view.ViewMode).Add(view);
        }

        internal bool CommitClose(View view)
        {
            return view != null && RemoveFromCollections(view);
        }

        internal View CommitBack()
        {
            var view = TopModal ?? CurrentPage;
            if (view != null)
            {
                CommitClose(view);
            }

            return view;
        }

        internal UIStackSnapshot Capture()
        {
            return new UIStackSnapshot(Pages, Modals, Widgets);
        }

        internal void Restore(UIStackSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            currentStack.Pages.Clear();
            currentStack.Pages.AddRange(snapshot.Pages);
            currentStack.Modals.Clear();
            currentStack.Modals.AddRange(snapshot.Modals);
            widgets.Clear();
            widgets.AddRange(snapshot.Widgets);
        }

        internal void Clear()
        {
            widgets.Clear();
            currentStack.Clear();
        }

        internal bool Contains(View view)
        {
            return view != null &&
                (currentStack.Pages.Contains(view) || currentStack.Modals.Contains(view) || widgets.Contains(view));
        }

        private List<View> GetCollection(UIViewMode viewMode)
        {
            return viewMode switch
            {
                UIViewMode.Page => currentStack.Pages,
                UIViewMode.Modal => currentStack.Modals,
                UIViewMode.Widget => widgets,
                _ => throw new ArgumentOutOfRangeException(nameof(viewMode), viewMode, null)
            };
        }

        private bool RemoveFromCollections(View view)
        {
            var removed = currentStack.Pages.Remove(view);
            removed |= currentStack.Modals.Remove(view);
            removed |= widgets.Remove(view);
            return removed;
        }

        private static View GetTop(IReadOnlyList<View> views)
        {
            return views.Count == 0 ? null : views[views.Count - 1];
        }

    }
}
