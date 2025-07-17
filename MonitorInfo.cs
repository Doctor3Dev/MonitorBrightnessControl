using System;

namespace Monitor
{
    /// <summary>
    /// A helper class to store information about a display monitor.
    /// </summary>
    public class MonitorInfo
    {
        public IntPtr HMonitor { get; set; } // Handle to the display monitor (from user32.dll).
        public string DeviceName { get; set; } // Device name (e.g., \\.\DISPLAY1).
        public IntPtr HPhysicalMonitor { get; set; } // Handle to the physical monitor (from Dxva2.dll).
        public string Description { get; set; } // Description of the physical monitor (e.g., "Generic PnP Monitor").

        public bool SupportsBrightnessControl { get; set; } // Indicates if brightness control is supported.
        public uint MinBrightness { get; set; } // Minimum brightness value.
        public uint MaxBrightness { get; set; } // Maximum brightness value.
        public uint CurrentBrightness { get; set; } // Current brightness value.

        public bool SupportsContrastControl { get; set; } // Indicates if contrast control is supported.
        public uint MinContrast { get; set; } // Minimum contrast value.
        public uint MaxContrast { get; set; } // Maximum contrast value.
        public uint CurrentContrast { get; set; } // Current contrast value.

        /// <summary>
        /// Overrides ToString() to provide a user-friendly display name for the ComboBox.
        /// </summary>
        public override string ToString()
        {
            // Combine description and device name for a more informative display.
            return $"{Description} ({DeviceName})";
        }
    }
}