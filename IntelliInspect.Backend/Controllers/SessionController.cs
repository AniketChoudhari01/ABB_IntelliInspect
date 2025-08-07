using System.Text.Json;
using IntelliInspect.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliInspect.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : ControllerBase
    {
        [HttpPost("ranges")]
        public IActionResult PostDateRanges([FromBody] DateRangeRequest request)
        {
            var storageDir = Path.Combine(Directory.GetCurrentDirectory(), "storage");
            var parsedCsvPath = Path.Combine(storageDir, "parsed.csv");
            var metadataPath = Path.Combine(storageDir, "metadata.json");

            if (!System.IO.File.Exists(parsedCsvPath) || !System.IO.File.Exists(metadataPath))
                return NotFound("Session data not found.");

            // Load metadata
            var metadataJson = System.IO.File.ReadAllText(metadataPath);
            var metadata = JsonSerializer.Deserialize<DatasetMetadata>(metadataJson)!;

            // 1. Validate ranges
            var validationResult = RangeValidator.Validate(request, metadata);
            if (!validationResult.IsValid)
                return BadRequest(new { status = "Invalid", message = validationResult.Message });
            Console.WriteLine("validated completed");
            // 2. Filter data by ranges
            var (train, test, sim, trainMonthly, testMonthly, simMonthly) =
                CsvRangeFilter.SplitCsvByRanges(parsedCsvPath, request);
            Console.WriteLine("Filtering completed");

            // 3. Save selection to session
            var rangeJson = JsonSerializer.Serialize(request);
            System.IO.File.WriteAllText(
                Path.Combine(storageDir, "range_selection.json"),
                rangeJson
            );
            Console.WriteLine("range selected completed");

            return Ok(
                new MonthlyDistributionResponse
                {
                    Status = "Valid",
                    TrainCount = train,
                    TestCount = test,
                    SimulateCount = sim,
                    TrainMonthly = trainMonthly,
                    TestMonthly = testMonthly,
                    SimMonthly = simMonthly,
                }
            );
        }

        //inject Iconfiguration in constructor
        private readonly IConfiguration _config;

        public SessionController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("train-model")]
        public async Task<IActionResult> TrainModel()
        {
            Console.WriteLine("backend ko call karo ");
            var fastApiUrl =
                _config["MLService:TrainUrl"] ?? "http://localhost:127.0.0.1:8000/train";

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10), // ⏳ Increase timeout to 10 minutes
            };

            Console.WriteLine($"Calling the ML service: {fastApiUrl}");

            try
            {
                var response = await httpClient.GetAsync(fastApiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error });
                }

                // Check for metrics.json in storage
                var metricsPath = Path.Combine("Storage", "metrics.json");
                if (!System.IO.File.Exists(metricsPath))
                {
                    return StatusCode(
                        500,
                        new
                        {
                            error = "Training completed, but metrics.json not found. Please train again.",
                        }
                    );
                }

                var metricsJson = await System.IO.File.ReadAllTextAsync(metricsPath);
                return Ok(new { message = "Training complete.", metrics = metricsJson });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception during training: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("simulate")]
        public async Task Simulate()
        {
            Response.ContentType = "text/event-stream";

            try
            {
                var fastApiUrl = $"http://127.0.0.1:8000/simulate";

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(60) };
                using var stream = await httpClient.GetStreamAsync(fastApiUrl);
                using var reader = new StreamReader(stream);
                using var writer = new StreamWriter(Response.Body);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        // Wrap properly for SSE
                        await writer.WriteLineAsync($"data: {line}\n");
                        await writer.WriteLineAsync(); // Empty line to terminate event
                        await writer.FlushAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Simulate error: {ex.Message}");
            }
        }
    }
}
