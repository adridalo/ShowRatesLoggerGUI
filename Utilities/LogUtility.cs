using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ShowRatesLoggerGUI.Models;

namespace ShowRatesLoggerGUI.Utilities
{
    internal class LogUtility
    {
        public static void Log(string filePath, string response, RateData data, bool showAll, bool csvOutput)
        {
            if (csvOutput)
            {
                if (showAll)
                {
                    var regex = new Regex(@"Window (\d+)\s+Render, Capture, Transfer: ([\d.]+), ([\d.]+), ([\d.]+)");
                    foreach(Match match in regex.Matches(response))
                    {
                        File.AppendAllText(filePath, $"{DateTime.Now},{match.Groups[1]},{match.Groups[2]},{match.Groups[3]},{match.Groups[4]}{Environment.NewLine}");
                    }
                }

                if(data != null)
                {
                    File.AppendAllText(filePath, $"{DateTime.Now},Average,{data.Render},{data.Capture},{data.Transfer}{Environment.NewLine}");
                }
            } else
            {
                File.AppendAllText(filePath, $"{DateTime.Now} || Render: {data?.Render}, Capture: {data?.Capture}, Transfer: {data?.Transfer}\n");
            }
        }
    }
}
