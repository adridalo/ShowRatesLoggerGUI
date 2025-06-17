using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShowRatesLoggerGUI.Models
{
    internal class RateData
    {
        public double Render { get; set; }
        public bool RenderNotificationsEnabled = false;
        public double Capture { get; set; }
        public bool CaptureNotificationsEnabled = false;
        public double Transfer { get; set; }
        public bool TransferNotificationsEnabled = false;
    }
}
