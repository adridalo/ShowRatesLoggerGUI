using System;
using System.Linq;
using System.Text.RegularExpressions;
using ShowRatesLoggerGUI.Models;

namespace ShowRatesLoggerGUI.Utilities
{
    internal class RateParser
    {
        public static RateData Parse(string response)
        {
            var line = response.Split(Environment.NewLine)
                .FirstOrDefault(line => line.StartsWith("Layout average"));

            if (line == null) return null;

            var matches = Regex.Matches(line, @"\d+\.\d+");
            if (matches.Count < 3) return null;

            return new RateData
            {
                Render = double.Parse(matches[0].Value),
                Capture = double.Parse(matches[1].Value),
                Transfer = double.Parse(matches[2].Value),
            };
        }

        public static string ShowRatesCleanOutput(string response)
        {
            string cleanedOutput = string.Join(Environment.NewLine,
                response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Where(line => !line.Trim().Equals("***showrates*** terminal"))
                         .Select(line => line.TrimStart('>'))
            );
            return cleanedOutput;
        }
    }
}
