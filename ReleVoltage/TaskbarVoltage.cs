
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class TaskbarVoltage : Form
{
    // WinAPI

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindowEx(
        IntPtr parent,
        IntPtr childAfter,
        string className,
        string windowName);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr GetParent(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern int MapWindowPoints(IntPtr from, IntPtr to, ref POINT point, uint points);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x, int y, int cx, int cy,
        uint flags);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT
    {
        public int X, Y;
    }

    const int WS_EX_NOACTIVATE = 0x08000000;
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const int WS_CHILD = unchecked((int)0x40000000);
    const int WS_CLIPSIBLINGS = 0x04000000;
    const int WS_POPUP = unchecked((int)0x80000000);

    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_SHOWWINDOW = 0x0040;
    // Fields

    private Timer positionTimer;

    private Point lastPosition;
    private bool hasPosition;
    private bool isHovered;

    private string currentVoltage = "--.- V";

    private readonly Font voltageFont =
        new Font("Tahoma", 12f, FontStyle.Regular);

    private Brush voltageBrush =
        new SolidBrush(Color.FromArgb(100, 230, 150));

    private readonly Brush hoverBrush =
        new SolidBrush(Color.FromArgb(60, 60, 60));

    private readonly ToolTip voltageToolTip = new ToolTip
    {
        ShowAlways = true
    };

    // Constructor

    public TaskbarVoltage()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;

        Size = new Size(85, 28);

        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        voltageToolTip.SetToolTip(this, "Текущее напряжение");

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true
        );

        positionTimer = new Timer
        {
            Interval = 100
        };

        positionTimer.Tick += (s, e) => UpdatePosition();
    }

    // Window styles

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;

            cp.Style &= ~WS_POPUP;
            cp.Style |= WS_CHILD | WS_CLIPSIBLINGS;
            cp.Parent = FindWindow("Shell_TrayWnd", null);

            cp.ExStyle |= WS_EX_NOACTIVATE;
            cp.ExStyle |= WS_EX_TOOLWINDOW;

            return cp;
        }
    }

    // Drawing

    protected override void OnMouseEnter(EventArgs e)
    {
        isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        isHovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.Clear(TransparencyKey);
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        if (isHovered)
        {
            Rectangle bounds = ClientRectangle;
            bounds.Inflate(-1, -1);
            int diameter = 10;

            using (var path = new GraphicsPath())
            {
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                e.Graphics.FillPath(hoverBrush, path);
            }
        }

        SizeF textSize = e.Graphics.MeasureString(currentVoltage, voltageFont);
        float x = (ClientSize.Width - textSize.Width) / 2;
        float y = (ClientSize.Height - textSize.Height) / 2;

        e.Graphics.DrawString(currentVoltage, voltageFont, voltageBrush, x, y);
    }

    // Set voltage

    public void SetVoltage(string voltage)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetVoltage(voltage)));
            return;
        }

        Color voltageColor = Color.Red;
        if (double.TryParse(voltage, NumberStyles.Float, CultureInfo.InvariantCulture, out double numericVoltage))
        {
            if (numericVoltage >= 215 && numericVoltage < 230)
                voltageColor = Color.FromArgb(100, 230, 150);
            else if ((numericVoltage >= 200 && numericVoltage < 215) ||
                     (numericVoltage >= 230 && numericVoltage < 245))
                voltageColor = Color.Orange;
        }

        voltageBrush.Dispose();
        voltageBrush = new SolidBrush(voltageColor);
        currentVoltage = voltage + " V";

        Invalidate();
    }

    // Position

    private void UpdatePosition()
    {
        IntPtr currentTaskbar = FindWindow("Shell_TrayWnd", null);
        if (currentTaskbar == IntPtr.Zero)
        {
            hasPosition = false;
            return;
        }

        if (!IsWindow(Handle))
        {
            RecreateHandle();
            hasPosition = false;
        }

        if (GetParent(Handle) != currentTaskbar)
        {
            SetParent(Handle, currentTaskbar);
            if (GetParent(Handle) != currentTaskbar)
                return;

            hasPosition = false;
        }

        IntPtr tray = FindWindowEx(
            currentTaskbar,
            IntPtr.Zero,
            "TrayNotifyWnd",
            null
        );

        if (tray == IntPtr.Zero)
            return;

        if (!GetWindowRect(tray, out RECT trayRect))
            return;

        int trayHeight = trayRect.Bottom - trayRect.Top;

        int x = trayRect.Left - Width - 8;

        int y = trayRect.Top + (trayHeight - Height) / 2;

        POINT position = new POINT { X = x, Y = y };
        MapWindowPoints(IntPtr.Zero, currentTaskbar, ref position, 1);
        x = position.X;
        y = position.Y;

        bool positionChanged = !hasPosition || lastPosition.X != x || lastPosition.Y != y;
        bool visible = IsWindowVisible(Handle);

        if (!positionChanged && visible)
            return;

        uint flags = SWP_NOACTIVATE | SWP_NOZORDER;
        if (!visible)
            flags |= SWP_SHOWWINDOW;

        if (!SetWindowPos(
            Handle,
            IntPtr.Zero,
            x, y,
            Width, Height,
            flags))
            return;

        lastPosition = new Point(x, y);
        hasPosition = true;
    }

    // Start

    public void StartMonitoring()
    {
        Show();
        UpdatePosition();
        positionTimer.Start();
    }

    // Dispose

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            positionTimer?.Stop();
            positionTimer?.Dispose();

            voltageFont?.Dispose();
            voltageBrush?.Dispose();
            hoverBrush?.Dispose();
            voltageToolTip?.Dispose();
        }

        base.Dispose(disposing);
    }
}
