using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class CsvDataModel
{
    public string[] Headers { get; private set; }
    public int NumColumns { get; private set; }
    public List<float[]> Rows { get; private set; }
    public float[] MinValues { get; private set; }
    public float[] MaxValues { get; private set; }

    public CsvDataModel(TextAsset csvFile)
    {
        string[] lines = csvFile.text.Split('\n');
        Headers = lines[0].Trim().Split(',');
        NumColumns = Headers.Length;

        Rows = new List<float[]>();
        MinValues = new float[NumColumns];
        MaxValues = new float[NumColumns];

        for (int col = 0; col < NumColumns; col++)
        {
            MinValues[col] = float.MaxValue;
            MaxValues[col] = float.MinValue;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] values = lines[i].Split(',');
            float[] row = new float[NumColumns];

            for (int col = 0; col < NumColumns; col++)
            {
                if (col >= values.Length) continue;
                if (float.TryParse(values[col], NumberStyles.Float,
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