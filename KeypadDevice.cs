using System;
using System.Runtime.InteropServices;

namespace ChipC_8
{
    public enum Key
    {
        None = 0,
        D1 = 35,
        D2 = 36,
        D3 = 37,
        D4 = 38,
        A = 44,
        C = 46,
        D = 47,
        E = 48,
        F = 49,
        Q = 60,
        R = 61,
        S = 62,
        V = 65,
        W = 66,
        X = 67,
        Z = 69
    }

    [Flags]
    public enum KeyStates : byte
    {
    	None = 0,
        Down = 1,
        Toggled = 2
    }

    public static class KeypadDevice
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern short GetKeyState(int keyCode);

        private static int VirtualKeyFromKey(Key key)
        {
	        return key switch
            {
                Key.D1 => 49,
                Key.D2 => 50,
                Key.D3 => 51,
                Key.D4 => 52,
                Key.A => 65,
                Key.C => 67,
                Key.D => 68,
                Key.E => 69,
                Key.F => 70,
                Key.Q => 81,
                Key.R => 82,
                Key.S => 83,
                Key.V => 86,
                Key.W => 87,
                Key.X => 88,
                Key.Z => 90,
                _ => default
	        };
        }

        /**/

        public static KeyStates GetKeyStates(Key key)
        {
	        if (key < Key.D1 || key > Key.Z)
	        {
		        throw new ArgumentOutOfRangeException(nameof(key));
	        }

        	KeyStates keyStates = KeyStates.None;

        	short keyState = GetKeyState(VirtualKeyFromKey(key));

        	if (((keyState >> 15) & 1) == 1)
        	{
        		keyStates |= KeyStates.Down;
        	}

        	if ((keyState & 1) == 1)
        	{
        		keyStates |= KeyStates.Toggled;
        	}

        	return keyStates;
        }

        public static bool IsKeyDown(Key key)
        {
        	return GetKeyStates(key).HasFlag(KeyStates.Down);
        }
    }
}