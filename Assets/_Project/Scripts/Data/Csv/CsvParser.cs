using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectH.Data.Csv
{
    public sealed class CsvTable
    {
        public CsvTable(string[] headers, List<Dictionary<string, string>> rows)
        {
            Headers = headers;
            Rows = rows;
        }

        public string[] Headers { get; }
        public List<Dictionary<string, string>> Rows { get; }
    }

    public static class CsvParser
    {
        public static CsvTable Parse(string text)
        {
            var rows = new List<Dictionary<string, string>>();
            var headers = Array.Empty<string>();
            var parsedHeader = false;

            if (string.IsNullOrWhiteSpace(text))
            {
                return new CsvTable(headers, rows);
            }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                {
                    continue;
                }

                var cols = ParseLine(rawLine);
                if (!parsedHeader)
                {
                    headers = cols.ConvertAll((c) => c.Trim()).ToArray();
                    parsedHeader = true;
                    continue;
                }

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headers.Length; i++)
                {
                    var v = i < cols.Count ? cols[i] : string.Empty;
                    row[headers[i]] = v?.Trim() ?? string.Empty;
                }

                rows.Add(row);
            }

            return new CsvTable(headers, rows);
        }

        private static List<string> ParseLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                else
                {
                    if (ch == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else if (ch == '"')
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
            }

            result.Add(sb.ToString());
            return result;
        }
    }
}