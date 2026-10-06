using System.Collections.Generic;
using System.Linq;
using AeroSim.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AeroSim.Input
{
    public static class FlightInput
    {
        public static Vector2 TouchLeft, TouchRight;
        public static List<AxisControl> JoystickAxes() => Joystick.current == null ? new List<AxisControl>() : Joystick.current.allControls.OfType<AxisControl>().Where(c => c is not ButtonControl).ToList();
        public static Vector4 Raw()
        {
            var result = new Vector4(TouchLeft.y, TouchLeft.x, TouchRight.y, TouchRight.x);
            if (Gamepad.current != null)
            {
                var l = Gamepad.current.leftStick.ReadValue(); var r = Gamepad.current.rightStick.ReadValue();
                result += new Vector4(l.y,l.x,r.y,r.x);
            }
            else if (Joystick.current != null)
            {
                var c = LocalStore.Profile.settings.calibration; var axes = JoystickAxes();
                float Read(int index) => axes.Count == 0 ? 0 : axes[Mathf.Clamp(index,0,axes.Count-1)].ReadValue();
                result += new Vector4(Read(c.throttleAxis),Read(c.yawAxis),Read(c.pitchAxis),Read(c.rollAxis));
            }
            var k = Keyboard.current;
            if (k != null)
            {
                float Axis(Key positive, Key negative) => (k[positive].isPressed?1:0)-(k[negative].isPressed?1:0);
                result += new Vector4(Axis(Key.R,Key.F),Axis(Key.E,Key.Q),Axis(Key.W,Key.S)+Axis(Key.UpArrow,Key.DownArrow),Axis(Key.D,Key.A)+Axis(Key.RightArrow,Key.LeftArrow));
                result.x += Axis(Key.PageUp,Key.PageDown);
            }
            for(int i=0;i<4;i++) result[i]=Mathf.Clamp(result[i],-1,1);
            return result;
        }
        public static Vector4 Read()
        {
            var raw=Raw(); var s=LocalStore.Profile.settings; var c=s.calibration;
            bool[] reverse={c.reverseThrottle,c.reverseYaw,c.reversePitch,c.reverseRoll};
            for(int i=0;i<4;i++)
            {
                float v=raw[i];
                if (Joystick.current != null && !string.IsNullOrEmpty(c.savedUtc))
                {
                    v-=c.center[i];
                    var range=v>=0?c.maximum[i]-c.center[i]:c.center[i]-c.minimum[i];
                    v/=Mathf.Max(.1f,range);
                }
                var absolute=Mathf.Abs(v); v=absolute<c.deadzone?0:Mathf.Sign(v)*(absolute-c.deadzone)/(1-c.deadzone);
                if (reverse[i]) v=-v;
                v=Mathf.Lerp(v,v*v*v,s.expo)*s.sensitivity;
                raw[i]=Mathf.Clamp(v,-1,1);
            }
            return raw;
        }
        public static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        public static bool PausePressed => Pressed(Key.Escape) || (Gamepad.current?.startButton.wasPressedThisFrame ?? false);
    }
}
