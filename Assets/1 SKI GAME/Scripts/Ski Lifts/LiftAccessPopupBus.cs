using System;
using UnityEngine;

namespace SkiGame.UI
{
    public static class LiftAccessPopupBus
    {
        public struct PopupMessage
        {
            public bool positive;
            public string title;
            public string body;
            public float seconds;
            public Color accent;
        }

        public static event Action<PopupMessage> OnPopup;

        public static void RaiseDenied(string requiredPassName, float seconds = 2.75f)
        {
            OnPopup?.Invoke(new PopupMessage
            {
                positive = false,
                title = "Lift Locked",
                body = $"Upgrade your pass to {requiredPassName} at the kiosk to use this ski lift",
                seconds = seconds,
                accent = new Color(0.92f, 0.34f, 0.34f, 1f)
            });
        }

        public static void RaiseAllowed(string passName, float seconds = 1.35f)
        {
            OnPopup?.Invoke(new PopupMessage
            {
                positive = true,
                title = "Lift Access Granted",
                body = $"Using {passName}",
                seconds = seconds,
                accent = new Color(0.28f, 0.76f, 0.46f, 1f)
            });
        }
    }
}