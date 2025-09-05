using Microsoft.AspNetCore.Mvc;
using FilesZipDownload.Models;
using FilesZipDownload.Services;
using Microsoft.AspNetCore.Http.Timeouts;

namespace FilesZipDownload.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DegreeController : ControllerBase
    {
        private readonly IExcelImportService _excelImportService;
        private readonly ILogger<DegreeController> _logger;

        public DegreeController(IExcelImportService excelImportService, ILogger<DegreeController> logger)
        {
            _excelImportService = excelImportService;
            _logger = logger;
        }

        [HttpPost("import")]
        [RequestSizeLimit(100_000_000)] // 100MB limit
        [RequestTimeout(600)] // 10 minutes timeout
        public async Task<ActionResult<ImportResponse>> ImportFromExcel(
            IFormFile file, 
            [FromForm] string createdBy = "system")
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new ImportResponse
                    {
                        Success = false,
                        Message = "No file uploaded or file is empty"
                    });
                }

                // Check file size (100MB limit for large datasets)
                if (file.Length > 100_000_000)
                {
                    return BadRequest(new ImportResponse
                    {
                        Success = false,
                        Message = "File size exceeds 100MB limit. Please split your data into smaller files."
                    });
                }

                // Check file extension
                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new ImportResponse
                    {
                        Success = false,
                        Message = "Invalid file format. Only Excel files (.xlsx, .xls) are allowed"
                    });
                }

                // Read Excel file
                _logger.LogInformation("Reading Excel file: {FileName}", file.FileName);
                var records = await _excelImportService.ReadExcelFileAsync(file);

                if (!records.Any())
                {
                    return BadRequest(new ImportResponse
                    {
                        Success = false,
                        Message = "No valid data found in the Excel file"
                    });
                }

                // Validate records
                _logger.LogInformation("Validating {RecordCount} records", records.Count);
                var validationResult = _excelImportService.ValidateRecords(records);

                // Save valid records
                int savedRecords = 0;
                if (validationResult.ValidRecords.Any())
                {
                    _logger.LogInformation("Saving {ValidRecordCount} valid records", validationResult.ValidRecords.Count);
                    savedRecords = await _excelImportService.SaveValidRecordsAsync(
                        validationResult.ValidRecords, 
                        createdBy);
                }

                // Prepare response
                var response = new ImportResponse
                {
                    Success = validationResult.ValidRecords.Any(),
                    Message = GetImportMessage(records.Count, validationResult, savedRecords),
                    TotalRecords = records.Count,
                    ValidRecords = validationResult.ValidRecords.Count,
                    InvalidRecords = validationResult.InvalidRecords.Count,
                    DuplicateRecords = validationResult.DuplicateRecords.Count,
                    SavedRecords = savedRecords,
                    ValidationDetails = validationResult
                };

                _logger.LogInformation("Import completed: {SavedRecords}/{TotalRecords} records saved", 
                    savedRecords, records.Count);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Excel import");
                return StatusCode(500, new ImportResponse
                {
                    Success = false,
                    Message = $"An error occurred during import: {ex.Message}"
                });
            }
        }

        [HttpPost("validate")]
        public async Task<ActionResult<ValidationResult>> ValidateExcel(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file uploaded or file is empty");
                }

                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest("Invalid file format. Only Excel files (.xlsx, .xls) are allowed");
                }

                var records = await _excelImportService.ReadExcelFileAsync(file);
                var validationResult = _excelImportService.ValidateRecords(records);

                return Ok(validationResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Excel validation");
                return StatusCode(500, $"An error occurred during validation: {ex.Message}");
            }
        }

        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            try
            {
                var headers = new[]
                {
                    "S No",
                    "Reg No", 
                    "Name",
                    "Term",
                    "Level",
                    "Degree",
                    "Degree Title",
                    "Degree Serial No",
                    "Conf",
                    "Program",
                    "Attended Period (Duration of Stay)",
                    "Passing Year"
                };

                var csvContent = string.Join(",", headers) + "\n";
                csvContent += "1,REG001,John Doe,Spring 2023,Bachelor,Computer Science,Bachelor of Science in Computer Science,12345,Confirmed,CS,2019-2023,2023\n";
                
                var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
                return File(bytes, "text/csv", "student_degree_template.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating template");
                return StatusCode(500, "An error occurred while generating template");
            }
        }

        private string GetImportMessage(int totalRecords, ValidationResult validationResult, int savedRecords)
        {
            var messages = new List<string>();

            if (savedRecords > 0)
            {
                messages.Add($"Successfully imported {savedRecords} out of {totalRecords} records");
            }

            if (validationResult.InvalidRecords.Any())
            {
                messages.Add($"{validationResult.InvalidRecords.Count} records failed validation");
            }

            if (validationResult.DuplicateRecords.Any())
            {
                messages.Add($"{validationResult.DuplicateRecords.Count} duplicate records found");
            }

            if (!messages.Any())
            {
                messages.Add("No records were processed");
            }

            return string.Join(". ", messages);
        }
    }
}
