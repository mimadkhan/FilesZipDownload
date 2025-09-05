using FilesZipDownload.Models;

namespace FilesZipDownload.Services
{
    public interface IExcelImportService
    {
        Task<List<ExcelImportDto>> ReadExcelFileAsync(IFormFile file);
        ValidationResult ValidateRecords(List<ExcelImportDto> records);
        Task<int> SaveValidRecordsAsync(List<ExcelImportDto> validRecords, string createdBy);
    }
}
