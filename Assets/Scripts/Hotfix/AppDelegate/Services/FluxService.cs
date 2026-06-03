using Core.Runtime;

namespace Hotfix.AppDelegate
{
    public static class FluxService
    {
        private static bool initialized;

        public static void InitializeGlobalData()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            GlobalData.Add<UserData>().InitData();
        }

        public static void ClearForRelogin()
        {
            GlobalData.ClearData();
            initialized = false;
        }
    }
}
