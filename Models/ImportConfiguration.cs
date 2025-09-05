namespace FilesZipDownload.Models
{
    public class ImportConfiguration
    {
        public int BatchSize { get; set; } = 10000;
        public int MaxParallelTasks { get; set; } = Environment.ProcessorCount;
        public int MaxFileSize { get; set; } = 100 * 1024 * 1024; // 100MB
        public int CommandTimeout { get; set; } = 300; // 5 minutes
        public bool UseStreamingMode { get; set; } = true;
        public bool EnableProgressReporting { get; set; } = true;
    }
}
