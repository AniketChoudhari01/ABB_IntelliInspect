namespace IntelliInspect.Backend.Models
{
    public class UploadResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
        public string? SessionId { get; set; }
        public DatasetMetadata? Metadata { get; set; }
    }
}
