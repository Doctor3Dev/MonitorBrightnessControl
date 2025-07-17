using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using Microsoft.Win32;
using Monitor;

/// <summary>
/// Main form class for the Monitor Brightness Control application.
/// This application allows users to adjust the brightness and contrast
/// of their monitors using Windows API calls (DDC/CI). It also provides
/// functionality to start the application with Windows and minimize to the system tray.
/// </summary>
public class MonitorBrightnessControl : Form
{
    // --- Constants for Monitor Capabilities ---
    private const uint MC_CAPS_BRIGHTNESS = 0x00000002;
    private const uint MC_CAPS_CONTRAST = 0x00000004;

    // --- Structures for P/Invoke ---
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public char[] szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MONITORINFOEX
    {
        public int cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
    }

    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll", EntryPoint = "EnumDisplayMonitors", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, [In, Out] MONITORINFOEX lpmi);

    [DllImport("Dxva2.dll", EntryPoint = "GetPhysicalMonitorsFromHMONITOR")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor, uint dwDesiredArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("Dxva2.dll", EntryPoint = "SetMonitorBrightness")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

    [DllImport("Dxva2.dll", EntryPoint = "GetMonitorBrightness")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

    [DllImport("Dxva2.dll", EntryPoint = "SetMonitorContrast")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetMonitorContrast(IntPtr hMonitor, uint dwNewContrast);

    [DllImport("Dxva2.dll", EntryPoint = "GetMonitorContrast")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorContrast(IntPtr hMonitor, out uint pdwMinimumContrast, out uint pdwCurrentContrast, out uint pdwMaximumContrast);

    [DllImport("Dxva2.dll", EntryPoint = "GetMonitorCapabilities")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorCapabilities(IntPtr hMonitor, out uint pdwMonitorCapabilities, out uint pdwSupportedColorTemperatures);

    [DllImport("Dxva2.dll", EntryPoint = "DestroyPhysicalMonitors")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    // --- Private Fields ---
    private List<MonitorInfo> _monitors = new List<MonitorInfo>();
    private MonitorInfo _selectedMonitor;
    private bool _monitorsLoaded = false;

    // --- UI Controls ---
    private ComboBox monitorComboBox;
    private TrackBar brightnessTrackBar;
    private Label brightnessLabel;
    private Label monitorLabel;
    private Label currentBrightnessValueLabel;
    private TrackBar contrastTrackBar;
    private Label contrastLabel;
    private Label currentContrastValueLabel;
    private NotifyIcon notifyIcon;
    private ContextMenuStrip contextMenuStrip;
    private GroupBox settingsGroupBox;
    private CheckBox startWithWindowsCheckBox;

    /// <summary>
    /// Constructor for the MonitorBrightnessControl form.
    /// Initializes the UI components and event handlers.
    /// </summary>
    public MonitorBrightnessControl()
    {
        InitializeComponent();
        this.Load += MonitorBrightnessUI_Load;
        this.FormClosing += MonitorBrightnessUI_FormClosing;
        this.Resize += MonitorBrightnessUI_Resize;
    }

    /// <summary>
    /// Initializes the UI components of the form.
    /// </summary>
    private void InitializeComponent()
    {
        this.Text = "Monitor Brightness Control via DDC/CI";
        this.Size = new Size(400, 360);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Font = new Font("Segoe UI", 10);

        monitorLabel = new Label { Text = "Select Monitor:", Location = new Point(20, 20), AutoSize = true };
        this.Controls.Add(monitorLabel);

        monitorComboBox = new ComboBox
        {
            Location = new Point(20, 40),
            Size = new Size(350, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        monitorComboBox.SelectedIndexChanged += MonitorComboBox_SelectedIndexChanged;
        this.Controls.Add(monitorComboBox);

        brightnessLabel = new Label { Text = "Brightness:", Location = new Point(20, 90), AutoSize = true };
        this.Controls.Add(brightnessLabel);

        currentBrightnessValueLabel = new Label { Text = "N/A", Location = new Point(100, 90), AutoSize = true };
        this.Controls.Add(currentBrightnessValueLabel);

        brightnessTrackBar = new TrackBar
        {
            Location = new Point(15, 120),
            Size = new Size(360, 45),
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            LargeChange = 10,
            SmallChange = 1,
            Enabled = false
        };
        brightnessTrackBar.Scroll += BrightnessTrackBar_Scroll;
        this.Controls.Add(brightnessTrackBar);

        contrastLabel = new Label { Text = "Contrast:", Location = new Point(20, 170), AutoSize = true };
        this.Controls.Add(contrastLabel);

        currentContrastValueLabel = new Label { Text = "N/A", Location = new Point(100, 170), AutoSize = true };
        this.Controls.Add(currentContrastValueLabel);

        contrastTrackBar = new TrackBar
        {
            Location = new Point(15, 200),
            Size = new Size(360, 45),
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            LargeChange = 10,
            SmallChange = 1,
            Enabled = false
        };
        contrastTrackBar.Scroll += ContrastTrackBar_Scroll;
        this.Controls.Add(contrastTrackBar);

        // Settings GroupBox
        settingsGroupBox = new GroupBox
        {
            Text = "Settings",
            Location = new Point(15, 250),
            Size = new Size(360, 60)
        };
        this.Controls.Add(settingsGroupBox);

        // Start With Windows CheckBox
        startWithWindowsCheckBox = new CheckBox
        {
            Text = "Start with Windows (Minimized)",
            Location = new Point(10, 25),
            AutoSize = true
        };
        startWithWindowsCheckBox.CheckedChanged += StartWithWindowsCheckBox_CheckedChanged;
        settingsGroupBox.Controls.Add(startWithWindowsCheckBox);

        // NotifyIcon and context menu
        notifyIcon = new NotifyIcon();
        notifyIcon.Text = "Monitor Brightness Control";
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
                notifyIcon.Icon = new Icon(iconPath);
            }
            else
            {
                this.Icon = SystemIcons.Application;
                notifyIcon.Icon = SystemIcons.Information;
            }
        }
        catch
        {
            this.Icon = SystemIcons.Application;
            notifyIcon.Icon = SystemIcons.Information;
        }
        notifyIcon.Visible = false;

        contextMenuStrip = new ContextMenuStrip();
        var showMenuItem = new ToolStripMenuItem("Show");
        showMenuItem.Click += (s, e) => ShowForm();
        contextMenuStrip.Items.Add(showMenuItem);

        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (s, e) => Application.Exit();
        contextMenuStrip.Items.Add(exitMenuItem);

        notifyIcon.ContextMenuStrip = contextMenuStrip;
        notifyIcon.DoubleClick += (s, e) => ShowForm();
    }

    /// <summary>
    /// Hides the form and shows the notify icon in the system tray.
    /// </summary>
    private void HideForm()
    {
        this.Hide();
        notifyIcon.Visible = true;
        this.ShowInTaskbar = false;
    }

    /// <summary>
    /// Shows the form and hides the notify icon.
    /// </summary>
    private void ShowForm()
    {
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.ShowInTaskbar = true;
        notifyIcon.Visible = false;
    }

    /// <summary>
    /// Enumerates all display monitors and collects their information.
    /// </summary>
    private void EnumerateMonitors()
    {
        _monitors.Clear();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumCallback, IntPtr.Zero);
    }

    #region Startup

    /// <summary>
    /// Sets the application to start with Windows by creating a registry key.
    /// </summary>
    private void SetStartupKey()
    {
        string appName = Application.ProductName;
        string appPath = Application.ExecutablePath;

        RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

        if (rk != null)
        {
            rk.SetValue(appName, $"\"{appPath}\" /Minimized");
            rk.Close();
        }
    }

    /// <summary>
    /// Deletes the registry key that makes the application start with Windows.
    /// </summary>
    private void DeleteStartupKey()
    {
        string appName = Application.ProductName;
        RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        if (rk != null && rk.GetValue(appName) != null)
        {
            rk.DeleteValue(appName, false);
            rk.Close();
        }
    }

    /// <summary>
    /// Checks if the startup key is set in the registry.
    /// </summary>
    /// <returns>True if the startup key is set, otherwise false.</returns>
    private bool IsStartupKeySet()
    {
        string appName = Application.ProductName;
        RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
        if (rk != null && rk.GetValue(appName) != null)
        {
            rk.Close();
            return true;
        }
        return false;
    }

    #endregion

    #region Callbacks

    /// <summary>
    /// Callback function for the EnumDisplayMonitors API.
    /// This method is called for each monitor found on the system.
    /// </summary>
    /// <param name="hMonitor">Handle to the display monitor.</param>
    /// <param name="hdcMonitor">Handle to a device context for the display monitor.</param>
    /// <param name="lprcMonitor">A pointer to a RECT structure that specifies the display monitor rectangle.</param>
    /// <param name="dwData">Application-defined data.</param>
    /// <returns>True to continue enumeration; false to stop.</returns>
    private bool MonitorEnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
    {
        MONITORINFOEX mi = new MONITORINFOEX();
        if (GetMonitorInfo(hMonitor, mi))
        {
            uint physicalMonitorCount = 1;
            PHYSICAL_MONITOR[] physicalMonitors = new PHYSICAL_MONITOR[physicalMonitorCount];
            if (GetPhysicalMonitorsFromHMONITOR(hMonitor, physicalMonitorCount, physicalMonitors))
            {
                foreach (var pm in physicalMonitors)
                {
                    var monitorInfo = new MonitorInfo
                    {
                        HMonitor = hMonitor,
                        DeviceName = mi.szDevice,
                        HPhysicalMonitor = pm.hPhysicalMonitor,
                        Description = new string(pm.szPhysicalMonitorDescription).TrimEnd('\0')
                    };

                    uint minVal = 0, currentVal = 0, maxVal = 0;
                    uint capabilities = 0, supportedColorTemperatures = 0;

                    if (GetMonitorCapabilities(pm.hPhysicalMonitor, out capabilities, out supportedColorTemperatures))
                    {
                        if ((capabilities & MC_CAPS_BRIGHTNESS) != 0)
                        {
                            monitorInfo.SupportsBrightnessControl = true;
                            if (GetMonitorBrightness(pm.hPhysicalMonitor, out minVal, out currentVal, out maxVal))
                            {
                                monitorInfo.MinBrightness = minVal;
                                monitorInfo.MaxBrightness = maxVal;
                                monitorInfo.CurrentBrightness = currentVal;
                            }
                        }
                        if ((capabilities & MC_CAPS_CONTRAST) != 0)
                        {
                            monitorInfo.SupportsContrastControl = true;
                            if (GetMonitorContrast(pm.hPhysicalMonitor, out minVal, out currentVal, out maxVal))
                            {
                                monitorInfo.MinContrast = minVal;
                                monitorInfo.MaxContrast = maxVal;
                                monitorInfo.CurrentContrast = currentVal;
                            }
                        }
                    }
                    _monitors.Add(monitorInfo);
                }
            }
        }
        return true;
    }

    #endregion

    #region UI Events

    /// <summary>
    /// Handles the Load event of the form.
    /// Enumerates monitors, populates the monitor combo box, and sets the initial state of the UI.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void MonitorBrightnessUI_Load(object sender, EventArgs e)
    {
        if (_monitorsLoaded)
            return;
        _monitorsLoaded = true;

        EnumerateMonitors();

        monitorComboBox.Items.Clear();
        foreach (var monitor in _monitors)
        {
            if (monitor.SupportsBrightnessControl || monitor.SupportsContrastControl)
                monitorComboBox.Items.Add(monitor);
        }

        if (monitorComboBox.Items.Count > 0)
        {
            // Select the first working monitor by default
            for (int i = 0; i < monitorComboBox.Items.Count; i++)
            {
                MonitorInfo monitor = monitorComboBox.Items[i] as MonitorInfo;
                if (monitor != null)
                {
                    uint capabilities = 0, supportedColorTemperatures = 0;
                    if (GetMonitorCapabilities(monitor.HPhysicalMonitor, out capabilities, out supportedColorTemperatures))
                    {
                        monitorComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
        else
        {
            monitorComboBox.Enabled = false;
            brightnessTrackBar.Enabled = false;
            contrastTrackBar.Enabled = false;
            MessageBox.Show("No monitors found that support software brightness or contrast control via DDC/CI.",
                "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Load Start with Windows setting
        startWithWindowsCheckBox.Checked = IsStartupKeySet();

        // Minimize to tray if the app is set to start with Windows
        if (IsStartupKeySet())
        {
            HideForm();
        }
    }

    /// <summary>
    /// Handles the FormClosing event of the form.
    /// Minimizes to the system tray if the user closes the form, or disposes of resources if the application is exiting.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">A FormClosingEventArgs that contains the event data.</param>
    private void MonitorBrightnessUI_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideForm();
        }
        if (e.CloseReason == CloseReason.ApplicationExitCall)
        {
            foreach (var monitor in _monitors)
            {
                if (monitor.HPhysicalMonitor != IntPtr.Zero)
                {
                    DestroyPhysicalMonitors(1, new PHYSICAL_MONITOR[] { new PHYSICAL_MONITOR { hPhysicalMonitor = monitor.HPhysicalMonitor } });
                }
            }
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }
    }

    /// <summary>
    /// Handles the Resize event of the form.
    /// Minimizes to the system tray when the form is minimized.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void MonitorBrightnessUI_Resize(object sender, EventArgs e)
    {
        if (this.WindowState == FormWindowState.Minimized)
        {
            HideForm();
        }
    }

    /// <summary>
    /// Handles the SelectedIndexChanged event of the monitor combo box.
    /// Updates the brightness and contrast track bars and labels based on the selected monitor.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void MonitorComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        _selectedMonitor = monitorComboBox.SelectedItem as MonitorInfo;
        if (_selectedMonitor != null)
        {
            // Brightness
            if (_selectedMonitor.SupportsBrightnessControl)
            {
                brightnessTrackBar.Enabled = true;
                brightnessTrackBar.Minimum = (int)_selectedMonitor.MinBrightness;
                brightnessTrackBar.Maximum = (int)_selectedMonitor.MaxBrightness;
                brightnessTrackBar.Value = (int)_selectedMonitor.CurrentBrightness;
                currentBrightnessValueLabel.Text = $"{_selectedMonitor.CurrentBrightness}%";
            }
            else
            {
                brightnessTrackBar.Enabled = false;
                brightnessTrackBar.Value = 0;
                currentBrightnessValueLabel.Text = "N/A";
            }
            // Contrast
            if (_selectedMonitor.SupportsContrastControl)
            {
                contrastTrackBar.Enabled = true;
                contrastTrackBar.Minimum = (int)_selectedMonitor.MinContrast;
                contrastTrackBar.Maximum = (int)_selectedMonitor.MaxContrast;
                contrastTrackBar.Value = (int)_selectedMonitor.CurrentContrast;
                currentContrastValueLabel.Text = $"{_selectedMonitor.CurrentContrast}%";
            }
            else
            {
                contrastTrackBar.Enabled = false;
                contrastTrackBar.Value = 0;
                currentContrastValueLabel.Text = "N/A";
            }
        }
    }

    /// <summary>
    /// Handles the Scroll event of the brightness track bar.
    /// Sets the brightness of the selected monitor and updates the brightness label.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void BrightnessTrackBar_Scroll(object sender, EventArgs e)
    {
        if (_selectedMonitor != null && _selectedMonitor.SupportsBrightnessControl)
        {
            uint newBrightness = (uint)brightnessTrackBar.Value;
            if (SetMonitorBrightness(_selectedMonitor.HPhysicalMonitor, newBrightness))
            {
                _selectedMonitor.CurrentBrightness = newBrightness;
                currentBrightnessValueLabel.Text = $"{newBrightness}%";
            }
            else
            {
                MessageBox.Show($"Failed to set brightness for '{_selectedMonitor.Description}'.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                brightnessTrackBar.Value = (int)_selectedMonitor.CurrentBrightness;
            }
        }
    }

    /// <summary>
    /// Handles the Scroll event of the contrast track bar.
    /// Sets the contrast of the selected monitor and updates the contrast label.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void ContrastTrackBar_Scroll(object sender, EventArgs e)
    {
        if (_selectedMonitor != null && _selectedMonitor.SupportsContrastControl)
        {
            uint newContrast = (uint)contrastTrackBar.Value;
            if (SetMonitorContrast(_selectedMonitor.HPhysicalMonitor, newContrast))
            {
                _selectedMonitor.CurrentContrast = newContrast;
                currentContrastValueLabel.Text = $"{newContrast}%";
            }
            else
            {
                MessageBox.Show($"Failed to set contrast for '{_selectedMonitor.Description}'.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                contrastTrackBar.Value = (int)_selectedMonitor.CurrentContrast;
            }
        }
    }

    /// <summary>
    /// Handles the CheckedChanged event of the start with Windows check box.
    /// Sets or deletes the startup key in the registry based on the check box state.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void StartWithWindowsCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        if (startWithWindowsCheckBox.Checked)
        {
            SetStartupKey();
        }
        else
        {
            DeleteStartupKey();
        }
    }

    #endregion
}