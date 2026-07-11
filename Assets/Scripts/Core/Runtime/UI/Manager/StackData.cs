using System.Collections.Generic;

namespace Core.Runtime
{
    internal sealed class StackData
    {
        internal readonly List<View> Pages = new List<View>();
        internal readonly List<View> Modals = new List<View>();
        internal string CustomName;

        internal int Count => Pages.Count + Modals.Count;

        internal void Clear()
        {
            Pages.Clear();
            Modals.Clear();
            CustomName = null;
        }
    }

    internal sealed class UIStackSnapshot
    {
        private readonly View[] pages;
        private readonly View[] modals;
        private readonly View[] widgets;

        internal UIStackSnapshot(
            IReadOnlyList<View> pages,
            IReadOnlyList<View> modals,
            IReadOnlyList<View> widgets)
        {
            this.pages = Copy(pages);
            this.modals = Copy(modals);
            this.widgets = Copy(widgets);
        }

        internal IReadOnlyList<View> Pages => pages;
        internal IReadOnlyList<View> Modals => modals;
        internal IReadOnlyList<View> Widgets => widgets;

        private static View[] Copy(IReadOnlyList<View> source)
        {
            var result = new View[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                result[i] = source[i];
            }

            return result;
        }
    }
}
