using UnityEngine;
using UnityEngine.InputSystem;

namespace SomeonesHome
{
    /// <summary>
    /// Devuelve el mando solo si de verdad se está usando.
    /// Un mando con "drift" o una lectura HID congelada (pasa con DualShock 4 en Linux/WebGL) deja el stick
    /// fijo en un valor distinto de cero y el jugador camina solo. El mando se ignora hasta que sus sticks
    /// se muevan respecto a la posición en la que estaban al conectarse.
    /// </summary>
    public static class GamepadGuard
    {
        static Gamepad pad;
        static Vector2 restLeft, restRight;
        static bool trusted;

        public static Gamepad Current
        {
            get
            {
                var g = Gamepad.current;
                if (g == null) return null;
                if (g != pad)
                {
                    pad = g;
                    restLeft = g.leftStick.ReadValue();
                    restRight = g.rightStick.ReadValue();
                    trusted = restLeft.sqrMagnitude < 0.04f && restRight.sqrMagnitude < 0.04f;
                    if (!trusted) Debug.LogWarning($"[Input] '{g.displayName}' tiene un stick fuera del centro ({restLeft}, {restRight}); se ignora hasta que se mueva.");
                }
                if (!trusted && ((g.leftStick.ReadValue() - restLeft).sqrMagnitude > 0.09f ||
                                 (g.rightStick.ReadValue() - restRight).sqrMagnitude > 0.09f))
                    trusted = true;
                return trusted ? g : null;
            }
        }
    }
}
