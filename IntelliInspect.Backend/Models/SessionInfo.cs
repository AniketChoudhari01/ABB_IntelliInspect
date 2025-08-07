namespace IntelliInspect.Backend.Models
{
    public class SessionInfo
    {
        public string SessionId { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ProcessedFilePath { get; set; } = string.Empty;
        public DatasetMetadata? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsProcessed { get; set; }
    }
}
