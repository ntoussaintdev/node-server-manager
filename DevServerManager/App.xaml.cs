using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using System.Windows.Interop;

namespace DevServerManager;

public partial class App : System.Windows.Application
{
    private const string InstanceName = "DevServerManager.4E370EB0-4AE4-41AC-818D-67FF3298AD8A";
    private const string ActivationEventName = "DevServerManager.Activate.4E370EB0-4AE4-41AC-818D-67FF3298AD8A";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private CancellationTokenSource? _activationCancellation;
    private Forms.NotifyIcon? _trayIcon;
    private Drawing.Icon? _appIcon;
    public bool IsExplicitExit { get; private set; }
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceName, out var isFirstInstance);
        _ownsMutex = isFirstInstance;
        if (!isFirstInstance)
        {
            try { EventWaitHandle.OpenExisting(ActivationEventName).Set(); } catch (WaitHandleCannotBeOpenedException) { }
            Shutdown();
            return;
        }
        base.OnStartup(e);
        _appIcon = CreateAppIcon();
        if (MainWindow is not null) MainWindow.Icon = Imaging.CreateBitmapSourceFromHIcon(_appIcon.Handle, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        CreateTrayIcon();
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _activationCancellation = new CancellationTokenSource();
        _ = Task.Run(() => WaitForActivation(_activationCancellation.Token));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
        _activationCancellation?.Cancel();
        _activationEvent?.Set();
        _activationEvent?.Dispose();
        if (_instanceMutex is not null)
        {
            if (_ownsMutex) _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
        }
        base.OnExit(e);
    }

    private void WaitForActivation(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _activationEvent?.WaitOne() == true)
        {
            if (cancellationToken.IsCancellationRequested) return;
            Dispatcher.Invoke(() =>
            {
                ShowManager();
            });
        }
    }

    public void ShowManager()
    {
        if (MainWindow is null) return;
        if (MainWindow.WindowState == WindowState.Minimized) MainWindow.WindowState = WindowState.Normal;
        MainWindow.Show(); MainWindow.Activate(); MainWindow.Topmost = true; MainWindow.Topmost = false; MainWindow.Focus();
    }

    public void ExitManager()
    {
        IsExplicitExit = true;
        Shutdown();
    }

    public void ShowTrayNotification() => _trayIcon?.ShowBalloonTip(2500, "Dev Server Manager is still running", "Use the tray icon to reopen the manager or exit and stop its servers.", Forms.ToolTipIcon.Info);

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open manager", null, (_, _) => Dispatcher.Invoke(ShowManager));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitManager));
        _trayIcon = new Forms.NotifyIcon { Icon = _appIcon, Text = "Dev Server Manager", ContextMenuStrip = menu, Visible = true };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowManager);
    }

    private static Drawing.Icon CreateAppIcon()
    {
        using var bitmap = new Drawing.Bitmap(64, 64);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.Clear(Drawing.Color.FromArgb(18, 18, 20));
            using var white = new Drawing.SolidBrush(Drawing.Color.White);
            using var font = new Drawing.Font("Consolas", 35, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            graphics.DrawString(">_", font, white, new Drawing.PointF(7, 13));
        }
        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }
}
