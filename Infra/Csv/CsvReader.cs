using System;
using System.Collections.Generic;
using System.IO;

namespace home_rental_tool.Infra.Csv
{
    public static class Csv
    {
        public static IEnumerable<string[]> Read(string path)
        {
            if (!File.Exists(path)) yield break;
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cells = new List<string>();
                var cur = "";
                var inQ = false;
                for (int i = 0; i < line.Length; i++)
                {
                    var ch = line[i];
                    if (ch == '\"') { inQ = !inQ; continue; }
                    if (ch == ',' && !inQ) { cells.Add(cur.Trim()); cur = ""; continue; }
                    cur += ch;
                }
                cells.Add(cur.Trim());
                yield return cells.ToArray();
            }
        }

        public static IEnumerable<T> SkipHeader<T>(this IEnumerable<string[]> rows, Func<string[], bool> isHeader, Func<string[], T> map)
        {
            foreach (var r in rows)
            {
                if (isHeader(r)) continue;
                yield return map(r);
            }
        }
    }
}
