using System;
using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Windows;

namespace Al_Khayr_Salat;

public partial class MainWindow : Window
{
    private TrayIcon? trayIcon;
    public static MainWindow Instance { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        this.Opened += OnOpened;
        this.Deactivated += OnLostFocus;
        CreateTrayIcon();
    }
    
    
    
    private void OnOpened(object? sender, EventArgs e)
    {
        var screen = Screens.Primary.WorkingArea;
        this.Position = new PixelPoint((int)(screen.Width - 400), (int)(screen.Height - 510));
        Console.WriteLine(screen.Width);
        Console.WriteLine(this.Width);
        updater = this.Find<TextBlock>("updater");
    }
    
    private void OnLostFocus(object? sender, EventArgs e)
    {
        this.Hide();
        ShowInSystemTray();
    }
    private void CreateTrayIcon()
    {
        trayIcon = new TrayIcon
        {
            Icon = new WindowIcon("Assets/logo.ico"),
            ToolTipText = "CornerApp"
        };

        trayIcon.Clicked += (sender, e) =>
        {
            this.Show();
            this.Activate();
        };
    }

    private void ShowInSystemTray()
    {
        if (trayIcon != null && !trayIcon.IsVisible)
        {
            trayIcon.IsVisible = true;
        }
    }
    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        e.Handled = true; // Block window movement
    }
}