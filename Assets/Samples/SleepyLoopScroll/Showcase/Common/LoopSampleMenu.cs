using UnityEngine;

namespace SleepyStudios.LoopScroll.Samples
{
    public sealed class LoopSampleMenu : LoopSamplePage
    {
        protected override string TitleKey => "menu";
        protected override void BuildPage()
        {
            ActionButton("language", new Vector2(365, 310), () => LoopSampleLanguage.SetEnglish(!LoopSampleLanguage.IsEnglish), new Vector2(180, 42));
            SetStatus("choose"); if (Catalog == null) { SetStatus("missingScene", "Catalog"); return; }
            var index = 0;
            foreach (var entry in Catalog.Entries)
            {
                if (entry.id == "menu") continue;
                var id = entry.id;
                var button = NavigationButton(entry.titleKey, new Vector2(0, 190 - index * 90), new Vector2(800, 78), () => NavigateTo(id));
                button.gameObject.name = "Navigate:" + id;
                var title = button.GetComponentInChildren<UnityEngine.UI.Text>(); title.rectTransform.sizeDelta = new Vector2(760, 35);
                title.rectTransform.anchoredPosition = new Vector2(0, 15); title.fontSize = 22;
                var description = LocalizedLabel(entry.descriptionKey, button.transform, new Vector2(760, 32), new Vector2(0, -19), 16);
                description.alignment = TextAnchor.MiddleCenter; index++;
            }
        }
    }
}
