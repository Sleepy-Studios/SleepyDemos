using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Core.Runtime
{
    internal sealed class StackData
    {
        internal readonly List<View> Pages = new List<View>();
        internal readonly List<View> Modals = new List<View>();
        internal readonly ReadOnlyCollection<View> ReadOnlyPages;
        internal readonly ReadOnlyCollection<View> ReadOnlyModals;

        internal StackData()
        {
            ReadOnlyPages = Pages.AsReadOnly();
            ReadOnlyModals = Modals.AsReadOnly();
        }

        internal int Count => Pages.Count + Modals.Count;

        internal void Clear()
        {
            Pages.Clear();
            Modals.Clear();
        }
    }

    internal sealed class UIStackSnapshot
    {
        private readonly ReadOnlyCollection<View> pages;
        private readonly ReadOnlyCollection<View> modals;
        private readonly ReadOnlyCollection<View> widgets;

        internal UIStackSnapshot(
            IReadOnlyList<View> pages,
            IReadOnlyList<View> modals,
            IReadOnlyList<View> widgets)
        {
            this.pages = Copy(pages).AsReadOnly();
            this.modals = Copy(modals).AsReadOnly();
            this.widgets = Copy(widgets).AsReadOnly();
        }

        internal IReadOnlyList<View> Pages => pages;
        internal IReadOnlyList<View> Modals => modals;
        internal IReadOnlyList<View> Widgets => widgets;

        internal bool Contains(View view)
        {
            return view != null && (pages.Contains(view) || modals.Contains(view) || widgets.Contains(view));
        }

        private static List<View> Copy(IReadOnlyList<View> source)
        {
            var result = new List<View>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(source[i]);
            }

            return result;
        }
    }
}
