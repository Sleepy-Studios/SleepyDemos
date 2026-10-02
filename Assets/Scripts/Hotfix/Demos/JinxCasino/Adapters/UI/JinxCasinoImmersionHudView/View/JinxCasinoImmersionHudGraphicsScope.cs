using System;

namespace Hotfix
{
    public partial class JinxCasinoImmersionHudView
    {
        private IDisposable graphicsEntrySuppression;

        private void SuppressGraphicsEntry() => graphicsEntrySuppression ??= GraphicsSettingsUI.SuppressEntry();
        private void ReleaseGraphicsEntrySuppression()
        { var previous = graphicsEntrySuppression; graphicsEntrySuppression = null; previous?.Dispose(); }
    }
}
