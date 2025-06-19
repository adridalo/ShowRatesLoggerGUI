using System;
using System.Collections.Generic;
using System.IO;
using ScottPlot;

namespace ShowRatesLoggerGUI.Services
{
    internal class GraphingService
    {
        public static void GeneratePlot(string dataPath, string dataLocation)
        {
            Plot plot = new Plot();
            var timeColumnValues = ExtractDateTimeColumn(dataPath);
            var renderColumnValues = ExtractRateColumn(dataPath, "Render");
            var captureColumnValues = ExtractRateColumn(dataPath, "Capture");
            var transferColumnValues = ExtractRateColumn(dataPath, "Transfer");

            var renderScatter = plot.Add.Scatter(timeColumnValues, renderColumnValues);
            var captureScatter = plot.Add.Scatter(timeColumnValues, captureColumnValues);
            var transferScatter = plot.Add.Scatter(timeColumnValues, transferColumnValues);

            renderScatter.LegendText = "Render";
            renderScatter.LineWidth = 15;
            captureScatter.LegendText = "Capture";
            captureScatter.LineWidth = 15;
            transferScatter.LegendText = "Transfer";
            transferScatter.LineWidth = 5;

            plot.Axes.DateTimeTicksBottom();
            plot.SavePng(
                Path.Join(dataLocation, "RatesGraph.png"),
                1920, 1080
            );
        }

        public static List<double> ExtractRateColumn(string dataPath, string columnName)
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

        public static List<DateTime> ExtractDateTimeColumn(string dataPath)
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
    }
}
