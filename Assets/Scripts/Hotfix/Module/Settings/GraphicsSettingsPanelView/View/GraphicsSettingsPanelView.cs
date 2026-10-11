using Core.Runtime;

namespace Hotfix
{
    /// ViewTab 拥有的独立设置分页，内部编辑器仅在显示期间绑定候选。
    [Module("Settings")]
    [UIBind("GraphicsSettingsPanelView")]
    public partial class GraphicsSettingsPanelView : SettingsPanelView
    {
        protected override SettingsPage Page => GraphicsSettingsPage_Page;
    }
}
