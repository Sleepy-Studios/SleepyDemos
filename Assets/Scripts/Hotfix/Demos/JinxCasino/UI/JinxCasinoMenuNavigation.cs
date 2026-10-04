using System.Collections.Generic;
using UnityEngine.UI;
namespace Hotfix.JinxCasino.UI
{
    internal static class JinxCasinoMenuNavigation
    {
        internal static void SaveNavigation(IList<Button> buttons)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                var navigation = buttons[i].navigation; navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = navigation.selectOnLeft = buttons[(i + buttons.Count - 1) % buttons.Count];
                navigation.selectOnDown = navigation.selectOnRight = buttons[(i + 1) % buttons.Count]; buttons[i].navigation = navigation;
            }
        }
    }
}
