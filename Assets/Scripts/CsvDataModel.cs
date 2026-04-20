using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace IARVR.Visualization
{
    /// <summary>
    /// Parses a CSV TextAsset into typed data on construction.
    /// Provides headers, per-row float values, and per-column min/max ranges.
    /// </summary>
    public class CsvDataModel
    {
        public string[]      Headers    { get; }
        public int           NumColumns { get; }
        public List<float[]> Rows       { get; }
        public float[]       MinValues  { get; }
        public float[]       MaxValues  { get; }

        public CsvDataModel(TextAsset csvFile)
        {
            string[] lines = csvFile.text.Split('\n');

            Headers    = lines[0].Trim().Split(',');
            NumColumns = Headers.Length;
            Rows       = new List<float[]>();
            MinValues  = new float[NumColumns];
            MaxValues  = new float[NumColumns];

            InitializeMinMax();
            ParseRows(lines);
        }

        // --- Private helpers ------------------------------------------------

        private void InitializeMinMax()
        {
            for (int col = 0; col < NumColumns; col++)
            {
                MinValues[col] = float.MaxValue;
                MaxValues[col] = float.MinValue;
            }
        }

        private void ParseRows(string[] lines)
        {
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                string[] cells = lines[i].Split(',');
                float[]  row   = new float[NumColumns];

                for (int col = 0; col < NumColumns; col++)
                {
                    if (col >= cells.Length) continue;

                    if (float.TryParse(cells[col], NumberStyles.Float,
                                       CultureInfo.InvariantCulture, out float val))
                    {
                        row[col] = val;
                        if (val < MinValues[col]) MinValues[col] = val;
                        if (val > MaxValues[col]) MaxValues[col] = val;
                    }
                }

                Rows.Add(row);
            }
        }
    }
}