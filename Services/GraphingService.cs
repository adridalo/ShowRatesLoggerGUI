using System;
using System.Collections.Generic;
using System.IO;
using ScottPlot;

namespace ShowRatesLoggerGUI.Services
{
    internal class GraphingService
    {
        public static void GeneratePlot(string dataPath, string dataContainingDirectory)
        {
            Plot plot = new Plot();
            var timeColumnValues = Path.GetExtension(dataPath).Equals(".csv") ? ExtractDateTimeColumnFromCsv(dataPath) : ExtractDateTimeColumnFromTxt(dataPath);
            var renderColumnValues = Path.GetExtension(dataPath).Equals(".csv") ? ExtractRateColumnFromCsv(dataPath, "Render") : ExtractRateColumnFromTxt(dataPath, "Render");
            var captureColumnValues = Path.GetExtension(dataPath).Equals(".csv") ? ExtractRateColumnFromCsv(dataPath, "Capture") : ExtractRateColumnFromTxt(dataPath, "Capture");
            var transferColumnValues = Path.GetExtension(dataPath).Equals(".csv") ? ExtractRateColumnFromCsv(dataPath, "Transfer") : ExtractRateColumnFromTxt(dataPath, "Transfer");

            var renderScatter = plot.Add.Scatter(timeColumnValues, renderColumnValues);
            var captureScatter = plot.Add.Scatter(timeColumnValues, captureColumnValues);
            var transferScatter = plot.Add.Scatter(timeColumnValues, transferColumnValues);

            renderScatter.LegendText = "Render";
            renderScatter.LineWidth = 10;

            captureScatter.LegendText = "Capture";
            captureScatter.LineWidth = 10;

            transferScatter.LegendText = "Transfer";
            transferScatter.LineWidth = 5;

            plot.Axes.DateTimeTicksBottom();
            plot.Title($"Rates average: {Path.GetFileName(dataPath)}");
            plot.SavePng(
                System.IO.Path.Join(dataContainingDirectory, "RatesGraph.png"),
                1920, 1080
            );
        }

        public static List<double> ExtractRateColumnFromTxt(string dataPath, string columnName)
        {
            string[] lines = File.ReadAllLines(dataPath);
            List<double> values = new();

            foreach (string line in lines)
            {
                if (!line.Contains("||")) continue;

                var parts = line.Split("||");
                if (parts.Length != 2) continue;

                string ratesData = parts[1];

                var ratesValues = ratesData.Split(",");

                foreach (var rateValue in ratesValues)
                {
                    var keyValue = rateValue.Split(":");
                    if (keyValue.Length != 2) continue;

                    string key = keyValue[0].Trim();
                    string value = keyValue[1].Trim();

                    if (key.Equals(columnName, StringComparison.OrdinalIgnoreCase) &&
                        double.TryParse(value, out double parsedRate))
                    {
                        values.Add(parsedRate);
                    }
                }
            }
            return values;
        }

        public static List<double> ExtractRateColumnFromCsv(string dataPath, string columnName)
        {
            string[] lines = File.ReadAllLines(dataPath);
            List<double> values = new();

            string[] headers = lines[0].Split(",");
            int colIndex = Array.IndexOf(headers, columnName);

            for (int i = 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split(",");
                if (parts.Length > colIndex && double.TryParse(parts[colIndex], out double val))
                {
                    values.Add(val);
                }
            }

            return values;
        }

        public static List<DateTime> ExtractDateTimeColumnFromCsv(string dataPath)
        {
            string[] lines = File.ReadAllLines(dataPath);
            List<DateTime> times = new();

            string[] headers = lines[0].Split(",");
            int colIndex = Array.IndexOf(headers, "Time");

            for (int i = 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split(",");
                if (parts.Length > colIndex && DateTime.TryParse(parts[colIndex], out DateTime dt))
                {
                    times.Add(dt);
                }
            }

            return times;
        }

        public static List<DateTime> ExtractDateTimeColumnFromTxt(string dataPath)
        {
            string[] lines = File.ReadAllLines(dataPath);
            List<DateTime> times = new();

            foreach (string line in lines)
                {
                if (!line.Contains("||")) continue;

                var parts = line.Split("||");
                if (parts.Length < 1) continue;

                string timestampPortion = parts[0].Trim();

                if (DateTime.TryParse(timestampPortion, out DateTime dt))
                    times.Add(dt);
            }

            return times;

        }
    }
}
