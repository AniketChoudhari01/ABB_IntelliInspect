using System.IO;
using IntelliInspect.Backend.Models;
using IntelliInspect.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class DatasetController : ControllerBase
{
    private readonly CsvParserService _csvParserService;

    public DatasetController(CsvParserService csvParserService)
    {
        _csvParserService = csvParserService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadCsv([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        if (Path.GetExtension(file.FileName)?.ToLower() != ".csv")
            return BadRequest("Only CSV files are allowed.");

    
        var storageDir = Path.Combine(Directory.GetCurrentDirectory(), "storage");
        Directory.CreateDirectory(storageDir);

        var rawPath = Path.Combine(storageDir, "raw.csv");
        var parsedPath = Path.Combine(storageDir, "parsed.csv");

     
        await using (var stream = new FileStream(rawPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

      
        var metadata = _csvParserService.ProcessCsv(rawPath, parsedPath);

  
        var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata);
        System.IO.File.WriteAllText(Path.Combine(storageDir, "metadata.json"), metadataJson);

        return Ok(new { message = "File processed successfully.", metadata });
    }
}
