using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using IntelliInspect.Backend.Models;

namespace IntelliInspect.Backend.Services;

public class CsvParserService
{
    public DatasetMetadata ProcessCsv(string inputPath, string outputPath)
    {
        Console.WriteLine("[INFO] Starting CSV processing...");

        var totalRows = 0;
        var responseOneCount = 0;
        int columnCount = 0;
        var startTimestamp = new DateTime(2021, 1, 1, 0, 0, 0);

        using var reader = new StreamReader(inputPath);
        using var writer = new StreamWriter(
            new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)
        );

        string? headerLine = reader.ReadLine();
        if (headerLine == null)
        {
            Console.WriteLine("[ERROR] CSV has no header.");
            throw new Exception("CSV has no header.");
        }

        var headers = headerLine.Split(',');
        columnCount = headers.Length;

        // Write augmented header to output
        writer.WriteLine($"{string.Join(",", headers)},SyntheticTimestamp");

        Console.WriteLine("[DEBUG] Header columns:");
        for (int i = 0; i < headers.Length; i++)
        {
            Console.WriteLine($"[{i}] '{headers[i]}'");
        }

        // Normalize headers and locate 'response' column
        int responseIndex = Array.FindIndex(headers, h => NormalizeHeader(h) == "response");

        if (responseIndex == -1)
        {
            Console.WriteLine("[WARN] 'Response' column not found.");
        }
        else
        {
            Console.WriteLine($"[INFO] 'Response' column found at index {responseIndex}");
        }

        int maxRowsToProcess = 300000;

        string? line;
        int lineNumber = 0;

        while ((line = reader.ReadLine()) != null && lineNumber < maxRowsToProcess)
        {
            lineNumber++;
            var fields = line.Split(',');

            if (fields.Length != columnCount)
            {
                Console.WriteLine(
                    $"[WARN] Malformed row at line {lineNumber}. Expected {columnCount} columns, got {fields.Length}. Skipping."
                );
                continue;
            }

            totalRows++;

            // Count Response == 1 if the column is valid
            if (responseIndex >= 0 && responseIndex < fields.Length)
            {
                var responseValue = fields[responseIndex].Trim();

                if (lineNumber <= 5)
                {
                    Console.WriteLine(
                        $"[DEBUG] Line {lineNumber} - Response Value: '{responseValue}'"
                    );
                }

                if (double.TryParse(responseValue, out double val) && val == 1)
                    responseOneCount++;
            }

            // Add synthetic timestamp
            var timestamp = startTimestamp.AddSeconds(lineNumber - 1);
            writer.WriteLine($"{line},{timestamp:O}");

            if (lineNumber % 10000 == 0)
                Console.WriteLine($"[INFO] Processed {lineNumber} rows...");
        }

        var endTimestamp = startTimestamp.AddSeconds(totalRows - 1);

        double passRate = totalRows > 0 ? (double)responseOneCount / totalRows * 100 : 0;

        // Console.WriteLine("[INFO] Finished processing CSV.");
        // Console.WriteLine($"[INFO] Total Rows: {totalRows}");
        // Console.WriteLine($"[INFO] Columns: {columnCount}");
        // Console.WriteLine($"[INFO] Response == 1 count: {responseOneCount}");
        // Console.WriteLine($"[INFO] Pass Rate: {passRate:F2}%");
        // Console.WriteLine($"[INFO] Timestamp Range: {startTimestamp:O} to {endTimestamp:O}");

      
        var metadata = new DatasetMetadata
        {
            RowCount = totalRows,
            ColumnCount = columnCount,
            PassRate = (passRate), // Already in percentage
            StartTimeStamp = startTimestamp,
            EndTimeStamp = endTimestamp,
        };

      
        var metadataJson = JsonSerializer.Serialize(metadata);
        File.WriteAllText(
            Path.Combine(Path.GetDirectoryName(outputPath)!, "metadata.json"),
            metadataJson
        );

      
        return metadata;
    }

    // Utility to remove invisible characters from headers
    private string NormalizeHeader(string header)
    {
        return header
            .Replace("\uFEFF", "")
            .Replace("\u00A0", "")
            .Replace("\u202f", "") 
            .Replace("\u200b", "") 
            .Trim()
            .ToLowerInvariant();
    }
}
