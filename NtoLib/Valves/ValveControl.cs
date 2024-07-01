﻿using FB.VisualFB;
using InSAT.Library.Gui;
using InSAT.Library.Interop.Win32;
using NtoLib.Render.Valves;
using NtoLib.Utils;
using NtoLib.Valves.Settings;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NtoLib.Valves
{
    [ComVisible(true)]
    [Guid("4641CB00-693D-46F2-85EC-5142B0C0EDA4")]
    [DisplayName("Клапан")]
    public partial class ValveControl : VisualControlBase
    {
        private Render.Orientation _orientation;
        [DisplayName("Ориентация клапана")]
        public Render.Orientation Orientation
        {
            get
            {
                return _orientation;
            }
            set
            {
                if(Math.Abs((int)_orientation - (int)value) % 180 == 90)
                    (Width, Height) = (Height, Width);

                bool updateRequired = _orientation != value;
                _orientation = value;
                if(updateRequired)
                    UpdateLayout();
            }
        }

        private ButtonOrientation _buttonOrientation;
        [DisplayName("Ориентация кнопок")]
        public ButtonOrientation ButtonOrientation
        {
            get
            {
                return _buttonOrientation;
            }
            set
            {
                bool updateRequired = _buttonOrientation != value;
                _buttonOrientation = value;
                if(updateRequired)
                    UpdateLayout();
            }
        }

        private bool _isSlideGate;
        [DisplayName("Шибер")]
        public bool IsSlideGate
        {
            get
            {
                return _isSlideGate;
            }
            set
            {
                _isSlideGate = value;

                if(_isSlideGate)
                {
                    if(_renderer.GetType() != typeof(SlideGateRenderer))
                        _renderer = new SlideGateRenderer(this);
                }
                else
                {
                    if(_isSmoothValve)
                    {
                        if(_renderer.GetType() != typeof(SmoothValveRenderer))
                            _renderer = new SmoothValveRenderer(this);
                    }
                    else
                    {
                        if(_renderer.GetType() != typeof(CommonValveRenderer))
                            _renderer = new CommonValveRenderer(this);
                    }
                }
            }
        }

        internal Status Status;
        private bool _previousSmoothValve;

        private ValveBaseRenderer _renderer;

        private SettingsForm _settingsForm;

        private bool _isSmoothValve = false;

        private Timer _impulseTimer;
        private int _currentCommand;

        private Timer _mouseHoldTimer;

        private Timer _animationTimer;
        private bool _animationClocker;



        public ValveControl()
        {
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();

            InitializeComponent();

            _renderer = new CommonValveRenderer(this);
        }

        protected override void ToRuntime()
        {
            base.ToRuntime();
            UpdateLayout();

            _impulseTimer = new Timer();
            _impulseTimer.Interval = 500;
            _impulseTimer.Tick += DisableCommandImpulse;

            _animationTimer = new Timer();
            _animationTimer.Interval = 500;
            _animationTimer.Tick += UpdateAnimation;

            _mouseHoldTimer = new Timer();
            _mouseHoldTimer.Interval = 200;
            _mouseHoldTimer.Tick += HandleMouseHoldDown;
        }

        protected override void ToDesign()
        {
            base.ToDesign();
            UpdateLayout();

            _settingsForm?.Close();

            _impulseTimer?.Dispose();
            _animationTimer?.Dispose();
            _mouseHoldTimer?.Dispose();
        }

        private void HandleResize(object sender, EventArgs e)
        {
            UpdateLayout();
        }



        private void HandleOpenClick(object sender, EventArgs e)
        {
            if(!Status.UsedByAutoMode && !Status.BlockOpening)
                SendCommand(ValveFB.OpenCmdId);
        }

        private void HandleOpenSmoothlyClick(object sender, EventArgs e)
        {
            if(!Status.UsedByAutoMode && !Status.BlockOpening)
                SendCommand(ValveFB.OpenSmoothlyCmdId);
        }

        private void HandleCloseClick(object sender, EventArgs e)
        {
            if(!Status.UsedByAutoMode && !Status.BlockClosing)
                SendCommand(ValveFB.CloseCmdId);
        }



        protected override void OnPaint(PaintEventArgs e)
        {
            if(!FBConnector.DesignMode)
                UpdateStatus();

            UpdateSprite();
        }

        private void UpdateAnimation(object sender, EventArgs e)
        {
            _animationClocker = !_animationClocker;
            UpdateSprite();
        }

        private void UpdateLayout()
        {
            DeviceLayout layout = LayoutBuilder.BuildLayout(this);
            spriteBox.Bounds = layout.DeviceRectangle;
            buttonTable.Bounds = layout.ButtonTableRectangle;

            UpdateButtonTable();
            UpdateSprite();
        }

        private void UpdateButtonTable()
        {
            Render.Orientation buttonsOrientation;
            if(IsHorizontal())
                buttonsOrientation = ButtonOrientation == ButtonOrientation.LeftTop ? Render.Orientation.Top : Render.Orientation.Bottom;
            else
                buttonsOrientation = ButtonOrientation == ButtonOrientation.LeftTop ? Render.Orientation.Left : Render.Orientation.Right;

            Button[] buttons;
            if(_isSmoothValve)
            {
                buttonOpenSmoothly.Visible = true;
                buttons = new Button[] { buttonOpen, buttonOpenSmoothly, buttonClose };
            }
            else
            {
                buttonOpenSmoothly.Visible = false;
                buttons = new Button[] { buttonOpen, buttonClose };
            }

            LayoutBuilder.RebuildTable(buttonTable, buttonsOrientation, buttons);
        }

        private void UpdateSprite()
        {
            spriteBox.Image = new Bitmap(Math.Max(1, spriteBox.Width), Math.Max(1, spriteBox.Height));

            using(var g = Graphics.FromImage(spriteBox.Image))
            {
                g.Clear(BackColor);

                GraphicsUnit unit = GraphicsUnit.Point;
                _renderer.Draw(g, spriteBox.Image.GetBounds(ref unit), Orientation, _animationClocker);
            }
        }

        private bool IsHorizontal()
        {
            return Orientation == Render.Orientation.Right || Orientation == Render.Orientation.Left;
        }



        private void HandleMouseDown(object sender, MouseEventArgs e)
        {
            if(e.Button != MouseButtons.Right)
            {
                _mouseHoldTimer.Stop();
                _settingsForm?.Close();
                return;
            }

            _mouseHoldTimer.Start();
        }

        private void HandleMouseHoldDown(object sender, EventArgs e)
        {
            OpenSettingsForm();
            _mouseHoldTimer.Stop();
        }

        private void HandleMouseUp(object sender, MouseEventArgs e)
        {
            if(e.Button != MouseButtons.Right)
                return;

            _mouseHoldTimer.Stop();
            _settingsForm?.Close();
        }

        private void StopHoldTimer(object sender, EventArgs e)
        {
            _mouseHoldTimer?.Stop();
        }



        private void HandleVisibleChanged(object sender, EventArgs e)
        {
            if(!Visible)
                _settingsForm?.Close();
        }

        private void DisableCommandImpulse(object sender, EventArgs e)
        {
            _impulseTimer.Stop();
            SetPinValue(_currentCommand, false);
        }



        private void SendCommand(int commandId)
        {
            if(_impulseTimer.Enabled)
            {
                DisableCommandImpulse(this, null);
                _impulseTimer.Stop();
            }

            _currentCommand = commandId;
            SetPinValue(_currentCommand, true);
            _impulseTimer.Start();
        }

        private void OpenSettingsForm()
        {
            _settingsForm = new SettingsForm(this);
            Point formLocation = MousePosition;
            Rectangle area = Screen.GetWorkingArea(formLocation);
            if(formLocation.X + _settingsForm.Width > area.Right)
                formLocation.X -= _settingsForm.Width;
            if(formLocation.Y + _settingsForm.Height > area.Bottom)
                formLocation.Y -= _settingsForm.Height;

            _settingsForm.Location = formLocation;
            _settingsForm.FormClosed += RemoveSettingsFormReference;
            _settingsForm.Show(Win32Window.FromInt32(User32.GetParent(Handle)));
        }

        private void RemoveSettingsFormReference(object sender, FormClosedEventArgs e)
        {
            SettingsForm form = (SettingsForm)sender;
            form.FormClosed -= RemoveSettingsFormReference;
            _settingsForm = null;
        }


        private void UpdateStatus()
        {
            Status.ConnectionOk = GetPinValue<bool>(ValveFB.ConnectionOkId);
            Status.NotOpened = GetPinValue<bool>(ValveFB.NotOpenedId);
            Status.NotClosed = GetPinValue<bool>(ValveFB.NotClosedId);
            Status.Collision = GetPinValue<bool>(ValveFB.CollisionId);
            Status.UsedByAutoMode = GetPinValue<bool>(ValveFB.UsedByAutoModeId);
            Status.Opened = GetPinValue<bool>(ValveFB.OpenedId);
            Status.OpenedSmoothly = GetPinValue<bool>(ValveFB.SmoothlyOpenedId);
            Status.Closed = GetPinValue<bool>(ValveFB.ClosedId);
            Status.OpeningClosing = GetPinValue<bool>(ValveFB.OpeningClosingId);

            Status.ForceClose = GetPinValue<bool>(ValveFB.ForceCloseId);
            Status.BlockClosing = GetPinValue<bool>(ValveFB.BlockClosingId);
            Status.BlockOpening = GetPinValue<bool>(ValveFB.BlockOpeningId);


            _isSmoothValve = GetPinValue<bool>(ValveFB.IsSmoothValveId);
            if(_isSmoothValve != _previousSmoothValve)
                UpdateLayout();
            _previousSmoothValve = _isSmoothValve;

            if(!IsSlideGate)
            {
                if(_isSmoothValve && _renderer.GetType() != typeof(SmoothValveRenderer))
                    _renderer = new SmoothValveRenderer(this);
                else if(!_isSmoothValve && _renderer.GetType() != typeof(CommonValveRenderer))
                    _renderer = new CommonValveRenderer(this);
            }

            bool animationNeeded = Status.OpeningClosing || (Status.Collision & !Status.OpenedSmoothly);
            if(!_animationTimer.Enabled && animationNeeded)
                _animationTimer.Start();
            if(_animationTimer.Enabled && !animationNeeded)
                _animationTimer.Stop();

            _settingsForm?.Invalidate();
        }

        private T GetPinValue<T>(int id)
        {
            return FBConnector.GetPinValue<T>(id + 1000);
        }

        private void SetPinValue<T>(int id, T value)
        {
            FBConnector.SetPinValue(id + 1000, value);
        }
    }
}
