using System;
using UnityEngine.InputSystem;

namespace AeroSim.Input
{
    public sealed class InputCapabilityService : IDisposable
    {
        public event Action CapabilitiesChanged;

        public bool HasGamepad => Gamepad.current != null;
        public bool HasJoystick => Joystick.current != null;
        public bool HasTouch => Touchscreen.current != null;
        public bool HasKeyboard => Keyboard.current != null;

        public string PrimaryDeviceLabel
        {
            get
            {
                if (HasGamepad) return Gamepad.current.displayName;
                if (HasJoystick) return Joystick.current.displayName;
                if (HasTouch) return "Touch Controls";
                if (HasKeyboard) return "Keyboard + Mouse";
                return UnityEngine.Application.isMobilePlatform ? "Touch Controls" : "Keyboard + Mouse";
            }
        }

        public void Start()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        public void Dispose()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change is InputDeviceChange.Added or InputDeviceChange.Removed or InputDeviceChange.Reconnected or InputDeviceChange.Disconnected)
            {
                CapabilitiesChanged?.Invoke();
            }
        }
    }
}
