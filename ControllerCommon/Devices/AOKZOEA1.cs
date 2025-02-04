﻿using ControllerCommon.Controllers;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class AOKZOEA1 : Device
    {
        public AOKZOEA1() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.ProductIllustration = "device_aokzoe_a1";
            this.ProductModel = "AOKZOEA1";

            // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u 
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 10, 28 };
            this.GfxClock = new double[] { 100, 2200 };

            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxis = new Vector3(-1.0f, 1.0f, -1.0f);
            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            // Home
            listeners.Add(new DeviceChord("Home",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D },
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D },
                false, ControllerButtonFlags.OEM1
                ));

            // Home (long press 1.5s)
            listeners.Add(new DeviceChord("Home, Long-press",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.G },
                new List<KeyCode>() { KeyCode.LWin, KeyCode.G },
                false, ControllerButtonFlags.OEM6
                ));

            // Keyboard
            listeners.Add(new DeviceChord("Keyboard",
                new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.O },
                new List<KeyCode>() { KeyCode.O, KeyCode.LWin, KeyCode.RControlKey },
                false, ControllerButtonFlags.OEM2
                ));

            // Turbo
            listeners.Add(new DeviceChord("Turbo",
                new List<KeyCode>() { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
                new List<KeyCode>() { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
                false, ControllerButtonFlags.OEM3
                ));

            // Home + Keyboard
            listeners.Add(new DeviceChord("Home + Keyboard",
                new List<KeyCode>() { KeyCode.RAlt, KeyCode.RControlKey, KeyCode.Delete },
                new List<KeyCode>() { KeyCode.Delete, KeyCode.RControlKey, KeyCode.RAlt },
                false, ControllerButtonFlags.OEM4
                ));

            // Home + Turbo
            listeners.Add(new DeviceChord("Home + Turbo",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.Snapshot },
                new List<KeyCode>() { KeyCode.Snapshot, KeyCode.LWin },
                false, ControllerButtonFlags.OEM5
                ));

            // Volume Up key (Single Press)
            listeners.Add(new DeviceChord("Volume Up key",
                new List<KeyCode>() { KeyCode.VolumeUp },
                new List<KeyCode>() { KeyCode.VolumeUp },
                false, ControllerButtonFlags.OEM7
                ));

            // Volume Down key (Single Press)
            listeners.Add(new DeviceChord("Volume Down key",
                new List<KeyCode>() { KeyCode.VolumeDown },
                new List<KeyCode>() { KeyCode.VolumeDown },
                false, ControllerButtonFlags.OEM8
                ));
        }
    }
}
