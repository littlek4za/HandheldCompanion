﻿using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using HandheldCompanion.Managers;
using neptune_hidapi.net;
using PrecisionTiming;
using SharpDX.XInput;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HandheldCompanion.Controllers
{
    public class NeptuneController : IController
    {
        #region imports
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);
        #endregion

        #region struct
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public SendInputEventType type;
            public MouseKeybdhardwareInputUnion mkhi;
        }
        [StructLayout(LayoutKind.Explicit)]
        struct MouseKeybdhardwareInputUnion
        {
            [FieldOffset(0)]
            public MouseInputData mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public int uMsg;
            public short wParamL;
            public short wParamH;
        }
        struct MouseInputData
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public MouseEventFlags dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        [Flags]
        enum MouseEventFlags : uint
        {
            MOUSEEVENTF_MOVE = 0x0001,
            MOUSEEVENTF_LEFTDOWN = 0x0002,
            MOUSEEVENTF_LEFTUP = 0x0004,
            MOUSEEVENTF_RIGHTDOWN = 0x0008,
            MOUSEEVENTF_RIGHTUP = 0x0010,
            MOUSEEVENTF_MIDDLEDOWN = 0x0020,
            MOUSEEVENTF_MIDDLEUP = 0x0040,
            MOUSEEVENTF_XDOWN = 0x0080,
            MOUSEEVENTF_XUP = 0x0100,
            MOUSEEVENTF_WHEEL = 0x0800,
            MOUSEEVENTF_VIRTUALDESK = 0x4000,
            MOUSEEVENTF_ABSOLUTE = 0x8000
        }
        enum SendInputEventType : int
        {
            InputMouse,
            InputKeyboard,
            InputHardware
        }
        #endregion

        private neptune_hidapi.net.NeptuneController Controller;
        private NeptuneControllerInputEventArgs input;

        private bool isConnected = false;
        private bool isVirtualMuted = false;

        private bool lastLeftHapticOn = false;
        private bool lastRightHapticOn = false;

        // temporary workaround
        private bool lastLeftPadClick = false;
        private bool lastRightPadClick = false;

        private NeptuneControllerInputState prevState;

        public NeptuneController(PnPDetails details)
        {
            Details = details;
            if (Details is null)
                return;

            Details.isHooked = true;

            Capacities |= ControllerCapacities.Gyroscope;
            Capacities |= ControllerCapacities.Accelerometer;

            HideOnHook = false;

            try
            {
                Controller = new();
                Controller.Open();
                isConnected = true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Couldn't initialize NeptuneController. Exception: {0}", ex.Message);
                return;
            }

            InputsTimer.Tick += (sender, e) => UpdateInputs();
            MovementsTimer.Tick += (sender, e) => UpdateMovements();

            bool LizardMouse = SettingsManager.GetBoolean("SteamDeckLizardMouse");
            SetLizardMouse(LizardMouse);

            bool LizardButtons = SettingsManager.GetBoolean("SteamDeckLizardButtons");
            SetLizardButtons(LizardButtons);

            bool Muted = SettingsManager.GetBoolean("SteamDeckMuteController");
            SetVirtualMuted(Muted);

            // ui
            DrawControls();
            RefreshControls();
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Details.Name))
                return Details.Name;
            return "Steam Controller Neptune";
        }

        public override void UpdateInputs()
        {
            if (input is null)
                return;

            if ((prevState is not null && input.State.GetHashCode() == prevState.GetHashCode()) && prevInjectedButtons == InjectedButtons)
                return;

            Inputs.Buttons = InjectedButtons;

            if (input.State.ButtonState[NeptuneControllerButton.BtnA])
                Inputs.Buttons |= ControllerButtonFlags.B1;
            if (input.State.ButtonState[NeptuneControllerButton.BtnB])
                Inputs.Buttons |= ControllerButtonFlags.B2;
            if (input.State.ButtonState[NeptuneControllerButton.BtnX])
                Inputs.Buttons |= ControllerButtonFlags.B3;
            if (input.State.ButtonState[NeptuneControllerButton.BtnY])
                Inputs.Buttons |= ControllerButtonFlags.B4;

            if (input.State.ButtonState[NeptuneControllerButton.BtnOptions])
                Inputs.Buttons |= ControllerButtonFlags.Start;
            if (input.State.ButtonState[NeptuneControllerButton.BtnMenu])
                Inputs.Buttons |= ControllerButtonFlags.Back;

            if (input.State.ButtonState[NeptuneControllerButton.BtnSteam])
                Inputs.Buttons |= ControllerButtonFlags.Special;
            if (input.State.ButtonState[NeptuneControllerButton.BtnQuickAccess])
                Inputs.Buttons |= ControllerButtonFlags.OEM1;

            var L2 = input.State.AxesState[NeptuneControllerAxis.L2] * byte.MaxValue / short.MaxValue;
            var R2 = input.State.AxesState[NeptuneControllerAxis.R2] * byte.MaxValue / short.MaxValue;

            if (L2 > Gamepad.TriggerThreshold)
                Inputs.Buttons |= ControllerButtonFlags.LeftTrigger;
            if (R2 > Gamepad.TriggerThreshold)
                Inputs.Buttons |= ControllerButtonFlags.RightTrigger;

            if (input.State.ButtonState[NeptuneControllerButton.BtnLStickPress])
                Inputs.Buttons |= ControllerButtonFlags.LeftThumb;
            if (input.State.ButtonState[NeptuneControllerButton.BtnRStickPress])
                Inputs.Buttons |= ControllerButtonFlags.RightThumb;

            if (input.State.ButtonState[NeptuneControllerButton.BtnLStickTouch])
                Inputs.Buttons |= ControllerButtonFlags.OEM2;
            if (input.State.ButtonState[NeptuneControllerButton.BtnRStickTouch])
                Inputs.Buttons |= ControllerButtonFlags.OEM3;

            if (input.State.ButtonState[NeptuneControllerButton.BtnL4])
                Inputs.Buttons |= ControllerButtonFlags.OEM4;
            if (input.State.ButtonState[NeptuneControllerButton.BtnL5])
                Inputs.Buttons |= ControllerButtonFlags.OEM5;

            if (input.State.ButtonState[NeptuneControllerButton.BtnR4])
                Inputs.Buttons |= ControllerButtonFlags.OEM6;
            if (input.State.ButtonState[NeptuneControllerButton.BtnR5])
                Inputs.Buttons |= ControllerButtonFlags.OEM7;

            if (input.State.ButtonState[NeptuneControllerButton.BtnL1])
                Inputs.Buttons |= ControllerButtonFlags.LeftShoulder;
            if (input.State.ButtonState[NeptuneControllerButton.BtnR1])
                Inputs.Buttons |= ControllerButtonFlags.RightShoulder;

            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadUp])
                Inputs.Buttons |= ControllerButtonFlags.DPadUp;
            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadDown])
                Inputs.Buttons |= ControllerButtonFlags.DPadDown;
            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadLeft])
                Inputs.Buttons |= ControllerButtonFlags.DPadLeft;
            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadRight])
                Inputs.Buttons |= ControllerButtonFlags.DPadRight;

            // Left Stick
            if (input.State.AxesState[NeptuneControllerAxis.LeftStickX] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickLeft;
            if (input.State.AxesState[NeptuneControllerAxis.LeftStickX] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickRight;

            if (input.State.AxesState[NeptuneControllerAxis.LeftStickY] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickDown;
            if (input.State.AxesState[NeptuneControllerAxis.LeftStickY] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickUp;

            Inputs.LeftThumbX = input.State.AxesState[NeptuneControllerAxis.LeftStickX];
            Inputs.LeftThumbY = input.State.AxesState[NeptuneControllerAxis.LeftStickY];

            // Right Stick
            if (input.State.AxesState[NeptuneControllerAxis.RightStickX] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickLeft;
            if (input.State.AxesState[NeptuneControllerAxis.RightStickX] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickRight;

            if (input.State.AxesState[NeptuneControllerAxis.RightStickY] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickDown;
            if (input.State.AxesState[NeptuneControllerAxis.RightStickY] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickUp;

            Inputs.RightThumbX = input.State.AxesState[NeptuneControllerAxis.RightStickX];
            Inputs.RightThumbY = input.State.AxesState[NeptuneControllerAxis.RightStickY];

            Inputs.LeftTrigger = L2;
            Inputs.RightTrigger = R2;

            Inputs.LeftPadX = short.MaxValue + input.State.AxesState[NeptuneControllerAxis.LeftPadX];
            Inputs.LeftPadY = short.MaxValue - input.State.AxesState[NeptuneControllerAxis.LeftPadY];
            Inputs.LeftPadTouch = input.State.ButtonState[NeptuneControllerButton.BtnLPadTouch];
            Inputs.LeftPadClick = input.State.ButtonState[NeptuneControllerButton.BtnLPadPress];

            Inputs.RightPadX = short.MaxValue + input.State.AxesState[NeptuneControllerAxis.RightPadX];
            Inputs.RightPadY = short.MaxValue - input.State.AxesState[NeptuneControllerAxis.RightPadY];
            Inputs.RightPadTouch = input.State.ButtonState[NeptuneControllerButton.BtnRPadTouch];
            Inputs.RightPadClick = input.State.ButtonState[NeptuneControllerButton.BtnRPadPress];

            if (Inputs.LeftPadTouch)
                Inputs.Buttons |= ControllerButtonFlags.OEM8;
            if (Inputs.LeftPadClick)
                Inputs.Buttons |= ControllerButtonFlags.OEM9;

            if (Inputs.RightPadTouch)
                Inputs.Buttons |= ControllerButtonFlags.OEM10;
            if (Inputs.RightPadClick)
                Inputs.Buttons |= ControllerButtonFlags.OEM11;

            // temporary workaround
            if (IsLizardMouseEnabled())
            {
                if (Inputs.LeftPadClick != lastLeftPadClick)
                {
                    INPUT mouseDownInput = new INPUT();
                    mouseDownInput.type = SendInputEventType.InputMouse;

                    switch (Inputs.LeftPadClick)
                    {
                        case true:
                            mouseDownInput.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_RIGHTDOWN;
                            break;
                        case false:
                            mouseDownInput.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_RIGHTUP;
                            break;
                    }

                    // send mouse input
                    SendInput(1, ref mouseDownInput, Marshal.SizeOf(new INPUT()));

                    lastLeftPadClick = Inputs.LeftPadClick;
                }

                if (Inputs.RightPadClick != lastRightPadClick)
                {
                    INPUT mouseDownInput = new INPUT();
                    mouseDownInput.type = SendInputEventType.InputMouse;

                    switch (Inputs.RightPadClick)
                    {
                        case true:
                            mouseDownInput.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_LEFTDOWN;
                            break;
                        case false:
                            mouseDownInput.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_LEFTUP;
                            break;
                    }

                    // send mouse input
                    SendInput(1, ref mouseDownInput, Marshal.SizeOf(new INPUT()));

                    lastRightPadClick = Inputs.RightPadClick;
                }
            }

            // update states
            prevState = input.State;

            base.UpdateInputs();
        }

        public override void UpdateMovements()
        {
            if (input is null)
                return;

            Movements.GyroAccelZ = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelY] / short.MaxValue * 2.0f;
            Movements.GyroAccelY = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelZ] / short.MaxValue * 2.0f;
            Movements.GyroAccelX = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelX] / short.MaxValue * 2.0f;

            Movements.GyroPitch = -(float)input.State.AxesState[NeptuneControllerAxis.GyroRoll] / short.MaxValue * 2000.0f;
            Movements.GyroRoll = (float)input.State.AxesState[NeptuneControllerAxis.GyroPitch] / short.MaxValue * 2000.0f;
            Movements.GyroYaw = -(float)input.State.AxesState[NeptuneControllerAxis.GyroYaw] / short.MaxValue * 2000.0f;

            base.UpdateMovements();
        }

        public override bool IsConnected()
        {
            return isConnected;
        }

        public bool IsLizardMouseEnabled()
        {
            return Controller.LizardMouseEnabled;
        }

        public bool IsLizardButtonsEnabled()
        {
            return Controller.LizardButtonsEnabled;
        }

        public virtual bool IsVirtualMuted()
        {
            return isVirtualMuted;
        }

        public override void Rumble(int loop)
        {
            new Thread(() =>
            {
                for (int i = 0; i < loop * 2; i++)
                {
                    if (i % 2 == 0)
                        SetVibration(byte.MaxValue, byte.MaxValue);
                    else
                        SetVibration(0, 0);

                    Thread.Sleep(100);
                }
            }).Start();

            base.Rumble(loop);
        }

        public override void Plug()
        {
            Controller.OnControllerInputReceived = input => Task.Run(() => OnControllerInputReceived(input));
            MovementsTimer.Start();

            PipeClient.ServerMessage += OnServerMessage;
            base.Plug();
        }

        private void OnControllerInputReceived(NeptuneControllerInputEventArgs input)
        {
            this.input = input;
        }

        public override void Unplug()
        {
            try
            {
                Controller.Close();
                isConnected = false;
            }
            catch
            {
                return;
            }
            
            MovementsTimer.Stop();
            
            PipeClient.ServerMessage -= OnServerMessage;
            base.Unplug();
        }

        public override void SetVibrationStrength(double value)
        {
            base.SetVibrationStrength(value);
            this.Rumble(1);
        }

        public override void SetVibration(byte LargeMotor, byte SmallMotor)
        {
            // todo: improve me
            // todo: https://github.com/mKenfenheuer/steam-deck-windows-usermode-driver/blob/69ce8085d3b6afe888cb2e36bd95836cea58084a/SWICD/Services/ControllerService.cs

            // Linear motors have a peak bell curve / s curve like responce, use left half, no linearization (yet?)
            // https://www.precisionmicrodrives.com/ab-003
            // Scale motor input request with user vibration strenth 0 to 100% accordingly

            byte AmplitudeLeft = (byte)(LargeMotor * VibrationStrength / byte.MaxValue * 12);

            bool leftHaptic = LargeMotor > 0;
            byte PeriodLeft = (byte)(30 - AmplitudeLeft);

            if (leftHaptic != lastLeftHapticOn)
            {
                _ = Controller.SetHaptic(1, (ushort)(leftHaptic ? AmplitudeLeft : 0), (ushort)(leftHaptic ? PeriodLeft : 0), 0);
                lastLeftHapticOn = leftHaptic;
            }

            byte AmplitudeRight = (byte)(SmallMotor * VibrationStrength / byte.MaxValue * 12);

            bool rightHaptic = SmallMotor > 0;
            byte PeriodRight = (byte)(30 - AmplitudeRight);

            if (rightHaptic != lastRightHapticOn)
            {
                _ = Controller.SetHaptic(0, (ushort)(rightHaptic ? AmplitudeRight : 0), (ushort)(rightHaptic ? PeriodRight : 0), 0);
                lastRightHapticOn = rightHaptic;
            }
        }

        private void OnServerMessage(PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_VIBRATION:
                    {
                        PipeClientVibration e = (PipeClientVibration)message;
                        SetVibration(e.LargeMotor, e.SmallMotor);
                    }
                    break;
            }
        }

        public void SetLizardMouse(bool lizardMode)
        {
            Controller.LizardMouseEnabled = lizardMode;
        }

        public void SetLizardButtons(bool lizardMode)
        {
            Controller.LizardButtonsEnabled = lizardMode;
        }

        public void SetVirtualMuted(bool mute)
        {
            isVirtualMuted = mute;
        }
    }
}
