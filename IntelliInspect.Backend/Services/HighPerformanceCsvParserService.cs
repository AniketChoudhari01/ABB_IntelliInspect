using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using IntelliInspect.Backend.Controllers; // Not needed, DatasetController is in the same assembly
using IntelliInspect.Backend.Models;

public class HighPerformanceCsvParserService
{
    private const int BUFFER_SIZE = 64 * 1024; // 64KB buffer
    private const int DEFAULT_BATCH_SIZE = 50_000; // Smaller batches for better memory usage
    private readonly ArrayPool<char> _charPool = ArrayPool<char>.Shared;

    public async Task<DatasetMetadata> ProcessCsvAsync(
        string inputPath,
        string outputPath,
        int batchSize = DEFAULT_BATCH_SIZE
    )
    {
        var stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"[INFO] Starting high-performance CSV processing: {inputPath}");

        // Quick row count using optimized method
        int totalRowCount = await CountRowsFastAsync(inputPath);
        Console.WriteLine(
            $"[INFO] Total rows: {totalRowCount:N0} (counted in {stopwatch.ElapsedMilliseconds}ms)"
        );

        if (totalRowCount == 0)
        {
            return CreateEmptyMetadata(inputPath);
        }

        // Reset stopwatch for processing
        stopwatch.Restart();

        // Prepare timestamp calculations
        var currentTime = DateTime.UtcNow;
        var startTimestamp = currentTime.AddSeconds(-totalRowCount + 1);

        // Use concurrent collections for thread-safe operations
        var stats = new ProcessingStats();
        string[]? headers = null;

        using var inputReader = new StreamReader(inputPath, Encoding.UTF8, false, BUFFER_SIZE);
        using var outputWriter = new StreamWriter(outputPath, false, Encoding.UTF8, BUFFER_SIZE);

        // Read and process headers
        var headerLine = await inputReader.ReadLineAsync();
        if (headerLine == null)
        {
            return CreateEmptyMetadata(inputPath);
        }

        headers = ParseCsvLine(headerLine);
        var augmentedHeaders = new string[headers.Length + 1];
        Array.Copy(headers, augmentedHeaders, headers.Length);
        augmentedHeaders[headers.Length] = "synthetic_timestamp";

        // Write augmented headers
        await outputWriter.WriteLineAsync(
            string.Join(",", augmentedHeaders.Select(h => EscapeCsvField(h)))
        );

        // Find Response column index for pass rate calculation
        int responseColumnIndex = Array.FindIndex(
            headers,
            h => string.Equals(h.Trim(), "Response", StringComparison.OrdinalIgnoreCase)
        );

        // Process in parallel batches
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount); // Limit concurrent batches
        var tasks = new List<Task>();
        var batchResults = new ConcurrentQueue<BatchResult>();

        var batch = new List<string>();
        string? line;
        int lineNumber = 0;

        while ((line = await inputReader.ReadLineAsync()) != null)
        {
            batch.Add(line);
            lineNumber++;

            if (batch.Count >= batchSize)
            {
                await semaphore.WaitAsync();
                var currentBatch = batch.ToArray(); // Create snapshot
                var batchStartIndex = lineNumber - batch.Count;
                batch.Clear();

                var task = ProcessBatchAsync(
                    currentBatch,
                    batchStartIndex,
                    startTimestamp,
                    responseColumnIndex,
                    headers.Length,
                    outputWriter,
                    semaphore,
                    batchResults
                );
                tasks.Add(task);

                // Progress reporting
                if (lineNumber % 100_000 == 0)
                {
                    Console.WriteLine(
                        $"[PROGRESS] Queued {lineNumber:N0}/{totalRowCount:N0} rows for processing ({stopwatch.ElapsedMilliseconds}ms)"
                    );
                }
            }
        }

        if (batch.Count > 0)
        {
            await semaphore.WaitAsync();
            var currentBatch = batch.ToArray();
            var batchStartIndex = lineNumber - batch.Count;

            var task = ProcessBatchAsync(
                currentBatch,
                batchStartIndex,
                startTimestamp,
                responseColumnIndex,
                headers.Length,
                outputWriter,
                semaphore,
                batchResults
            );
            tasks.Add(task);
        }

    
        await Task.WhenAll(tasks);

    
        var totalResponseOnes = 0;
        DateTime? firstTimestamp = null;
        DateTime? lastTimestamp = null;

        while (batchResults.TryDequeue(out var result))
        {
            totalResponseOnes += result.ResponseOnes;

            if (firstTimestamp == null || result.FirstTimestamp < firstTimestamp)
                firstTimestamp = result.FirstTimestamp;

            if (lastTimestamp == null || result.LastTimestamp > lastTimestamp)
                lastTimestamp = result.LastTimestamp;
        }

        stopwatch.Stop();

        var metadata = new DatasetMetadata
        {
            FileName = Path.GetFileName(inputPath),
            RowCount = totalRowCount,
            ColumnCount = headers.Length + 1, // +1 for synthetic timestamp
            PassRate = totalRowCount == 0 ? 0 : (double)totalResponseOnes / totalRowCount * 100,
            StartTimeStamp = firstTimestamp ?? currentTime,
            EndTimeStamp = lastTimestamp ?? currentTime,
        };

        Console.WriteLine(
            $"[SUCCESS] Processing complete in {stopwatch.ElapsedMilliseconds:N0}ms!"
        );
        Console.WriteLine($"     File: {metadata.FileName}");
        Console.WriteLine($"     Rows: {metadata.RowCount:N0}");
        Console.WriteLine($"  Columns: {metadata.ColumnCount}");
        Console.WriteLine($" PassRate: {metadata.PassRate:F2}%");
        Console.WriteLine($"StartTime: {metadata.StartTimeStamp}");
        Console.WriteLine($"  EndTime: {metadata.EndTimeStamp}");
        Console.WriteLine(
            $"Throughput: {(double)totalRowCount / stopwatch.ElapsedMilliseconds * 1000:N0} rows/second"
        );

        return metadata;
    }

    private async Task ProcessBatchAsync(
        string[] lines,
        int startIndex,
        DateTime baseTimestamp,
        int responseColumnIndex,
        int expectedColumnCount,
        StreamWriter outputWriter,
        SemaphoreSlim semaphore,
        ConcurrentQueue<BatchResult> results
    )
    {
        try
        {
            var stringBuilder = new StringBuilder(lines.Length * 200); // Pre-allocate
            var responseOnes = 0;
            DateTime? firstTimestamp = null;
            DateTime? lastTimestamp = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = ParseCsvLine(line);
                var timestamp = baseTimestamp.AddSeconds(startIndex + i);

                if (firstTimestamp == null)
                    firstTimestamp = timestamp;
                lastTimestamp = timestamp;

                // Check response field
                if (responseColumnIndex >= 0 && responseColumnIndex < fields.Length)
                {
                    if (
                        string.Equals(
                            fields[responseColumnIndex]?.Trim(),
                            "1",
                            StringComparison.Ordinal
                        )
                    )
                    {
                        responseOnes++;
                    }
                }

               
                for (int j = 0; j < Math.Min(fields.Length, expectedColumnCount); j++)
                {
                    if (j > 0)
                        stringBuilder.Append(',');
                    stringBuilder.Append(EscapeCsvField(fields[j]));
                }

              
                stringBuilder.Append(',');
                stringBuilder.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                stringBuilder.AppendLine();
            }

           
            lock (outputWriter)
            {
                outputWriter.Write(stringBuilder.ToString());
                outputWriter.Flush();
            }

            results.Enqueue(
                new BatchResult
                {
                    ResponseOnes = responseOnes,
                    FirstTimestamp = firstTimestamp ?? DateTime.UtcNow,
                    LastTimestamp = lastTimestamp ?? DateTime.UtcNow,
                }
            );
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<int> CountRowsFastAsync(string path)
    {
        const int bufferSize = 1024 * 1024; // 1MB buffer
        var buffer = new byte[bufferSize];
        var lineCount = 0;

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize,
            FileOptions.SequentialScan
        );

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == '\n')
                    lineCount++;
            }
        }

        // Subtract header line
        return Math.Max(0, lineCount - 1);
    }

    private string[] ParseCsvLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return Array.Empty<string>();

        var fields = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentField.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        fields.Add(currentField.ToString());
        return fields.ToArray();
    }

    private string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (
            field.Contains(',')
            || field.Contains('"')
            || field.Contains('\n')
            || field.Contains('\r')
        )
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    private DatasetMetadata CreateEmptyMetadata(string inputPath)
    {
        return new DatasetMetadata
        {
            FileName = Path.GetFileName(inputPath),
            RowCount = 0,
            ColumnCount = 0,
            PassRate = 0,
            StartTimeStamp = DateTime.UtcNow,
            EndTimeStamp = DateTime.UtcNow,
        };
    }

    private class ProcessingStats
    {
        public int TotalRows;
        public int ResponseOnes;
        public DateTime? FirstTimestamp;
        public DateTime? LastTimestamp;
    }

    private class BatchResult
    {
        public int ResponseOnes { get; set; }
        public DateTime FirstTimestamp { get; set; }
        public DateTime LastTimestamp { get; set; }
    }
}

// Extension to the controller to use the high-performance parser
namespace IntelliInspect.Backend.Controllers
{
    public static class ControllerExtensions
    {
        public static async Task<SessionInfo> ProcessCsvFileHighPerformanceAsync(
            this DatasetController controller,
            IFormFile file,
            string sessionId
        )
        {
            var baseStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "storage");
            var sessionFolder = Path.Combine(baseStoragePath, sessionId);
            Directory.CreateDirectory(sessionFolder);

            var originalFileName = file.FileName;
            var inputPath = Path.Combine(sessionFolder, originalFileName);
            var outputPath = Path.Combine(sessionFolder, "processed.csv");

            // Save uploaded file
            using (
                var stream = new FileStream(
                    inputPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024
                )
            )
            {
                await file.CopyToAsync(stream);
            }

           
            var parser = new HighPerformanceCsvParserService();
            var metadata = await parser.ProcessCsvAsync(inputPath, outputPath);

         
            var sessionInfo = new SessionInfo
            {
                SessionId = sessionId,
                OriginalFileName = originalFileName,
                ProcessedFilePath = outputPath,
                Metadata = metadata,
                CreatedAt = DateTime.UtcNow,
                IsProcessed = true,
            };

            return sessionInfo;
        }
    }
}
