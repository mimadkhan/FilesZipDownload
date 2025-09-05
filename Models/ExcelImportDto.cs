namespace FilesZipDownload.Models
{
    public class ExcelImportDto
    {
        public int SNo { get; set; }
        public string RegNo { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Term { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Degree { get; set; } = string.Empty;
        public string DegreeTitle { get; set; } = string.Empty;
        public int DegreeSerialNo { get; set; }
        public string Conf { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public string AttendedPeriod { get; set; } = string.Empty;
        public string PassingYear { get; set; } = string.Empty;
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<ExcelImportDto> ValidRecords { get; set; } = new List<ExcelImportDto>();
        public List<DuplicateRecord> DuplicateRecords { get; set; } = new List<DuplicateRecord>();
        public List<InvalidRecord> InvalidRecords { get; set; } = new List<InvalidRecord>();
    }

    public class DuplicateRecord
    {
        public int RowNumber { get; set; }
        public ExcelImportDto Record { get; set; } = new ExcelImportDto();
        public string DuplicateReason { get; set; } = string.Empty;
    }

    public class InvalidRecord
    {
        public int RowNumber { get; set; }
        public ExcelImportDto Record { get; set; } = new ExcelImportDto();
        public List<string> ValidationErrors { get; set; } = new List<string>();
    }

    public class ImportResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalRecords { get; set; }
        public int ValidRecords { get; set; }
        public int InvalidRecords { get; set; }
        public int DuplicateRecords { get; set; }
        public int SavedRecords { get; set; }
        public ValidationResult ValidationDetails { get; set; } = new ValidationResult();
    }
}
