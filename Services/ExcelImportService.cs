using FilesZipDownload.Models;
using System.Data;
using OfficeOpenXml;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace FilesZipDownload.Services
{
    public class ExcelImportService : IExcelImportService
    {
        private const int BATCH_SIZE = 10000;
        private static readonly int MAX_PARALLEL_TASKS = Environment.ProcessorCount;
        
        private readonly Dictionary<string, int> _columnMapping = new Dictionary<string, int>
        {
            { "S No", 0 },
            { "Reg No", 1 },
            { "Name", 2 },
            { "Term", 3 },
            { "Level", 4 },
            { "Degree", 5 },
            { "Degree Title", 6 },
            { "Degree Serial No", 7 },
            { "Conf", 8 },
            { "Program", 9 },
            { "Attended Period (Duration of Stay)", 10 },
            { "Passing Year", 11 }
        };

        public async Task<List<ExcelImportDto>> ReadExcelFileAsync(IFormFile file)
        {
            var records = new ConcurrentBag<ExcelImportDto>();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets[0];

            var rowCount = worksheet.Dimension?.Rows ?? 0;
            var colCount = worksheet.Dimension?.Columns ?? 0;

            // Process in parallel batches for better performance
            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(MAX_PARALLEL_TASKS);

            for (int startRow = 2; startRow <= rowCount; startRow += BATCH_SIZE)
            {
                var endRow = Math.Min(startRow + BATCH_SIZE - 1, rowCount);
                
                tasks.Add(ProcessRowBatchAsync(worksheet, startRow, endRow, colCount, records, semaphore));
            }

            await Task.WhenAll(tasks);
            return records.ToList();
        }

        private async Task ProcessRowBatchAsync(ExcelWorksheet worksheet, int startRow, int endRow, int colCount, 
            ConcurrentBag<ExcelImportDto> records, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    for (int row = startRow; row <= endRow; row++)
                    {
                        var record = ProcessSingleRow(worksheet, row, colCount);
                        if (record != null)
                        {
                            records.Add(record);
                        }
                    }
                });
            }
            finally
            {
                semaphore.Release();
            }
        }

        private ExcelImportDto? ProcessSingleRow(ExcelWorksheet worksheet, int row, int colCount)
        {
            bool isEmptyRow = true;
            
            // Quick check for empty row using range
            var range = worksheet.Cells[row, 1, row, colCount];
            foreach (var cell in range)
            {
                if (cell.Value != null && !string.IsNullOrWhiteSpace(cell.Value.ToString()))
                {
                    isEmptyRow = false;
                    break;
                }
            }

            if (isEmptyRow) return null;

            // Optimized cell reading with direct indexing
            return new ExcelImportDto
            {
                SNo = GetIntValue(worksheet.Cells[row, 1]),
                RegNo = GetStringValue(worksheet.Cells[row, 2]),
                Name = GetStringValue(worksheet.Cells[row, 3]),
                Term = GetStringValue(worksheet.Cells[row, 4]),
                Level = GetStringValue(worksheet.Cells[row, 5]),
                Degree = GetStringValue(worksheet.Cells[row, 6]),
                DegreeTitle = GetStringValue(worksheet.Cells[row, 7]),
                DegreeSerialNo = GetIntValue(worksheet.Cells[row, 8]),
                Conf = GetStringValue(worksheet.Cells[row, 9]),
                Program = GetStringValue(worksheet.Cells[row, 10]),
                AttendedPeriod = GetStringValue(worksheet.Cells[row, 11]),
                PassingYear = GetStringValue(worksheet.Cells[row, 12])
            };
        }

        private static string GetStringValue(ExcelRange cell)
        {
            return cell.Value?.ToString()?.Trim() ?? string.Empty;
        }

        private static int GetIntValue(ExcelRange cell)
        {
            return int.TryParse(cell.Value?.ToString(), out int value) ? value : 0;
        }

        public ValidationResult ValidateRecords(List<ExcelImportDto> records)
        {
            var result = new ValidationResult();
            var duplicateCheck = new ConcurrentDictionary<string, int>();
            var validRecords = new ConcurrentBag<ExcelImportDto>();
            var invalidRecords = new ConcurrentBag<InvalidRecord>();
            var duplicateRecords = new ConcurrentBag<DuplicateRecord>();

            // Process validation in parallel batches
            var indexedRecords = records.Select((record, index) => new { Record = record, Index = index }).ToList();
            
            Parallel.ForEach(indexedRecords, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLEL_TASKS }, item =>
            {
                var record = item.Record;
                var rowNumber = item.Index + 2; // Starting from row 2 (after header)
                var validationErrors = ValidateRecord(record);

                // Check for duplicates based on RegNo + DegreeSerialNo
                var duplicateKey = $"{record.RegNo}_{record.DegreeSerialNo}";
                var existingRowNumber = duplicateCheck.GetOrAdd(duplicateKey, rowNumber);
                
                if (existingRowNumber != rowNumber)
                {
                    duplicateRecords.Add(new DuplicateRecord
                    {
                        RowNumber = rowNumber,
                        Record = record,
                        DuplicateReason = $"Duplicate combination of Reg No ({record.RegNo}) and Degree Serial No ({record.DegreeSerialNo})"
                    });
                }
                else if (validationErrors.Any())
                {
                    invalidRecords.Add(new InvalidRecord
                    {
                        RowNumber = rowNumber,
                        Record = record,
                        ValidationErrors = validationErrors
                    });
                }
                else
                {
                    validRecords.Add(record);
                }
            });

            // Convert concurrent collections to regular lists
            result.ValidRecords = validRecords.ToList();
            result.InvalidRecords = invalidRecords.ToList();
            result.DuplicateRecords = duplicateRecords.ToList();
            
            result.IsValid = result.ValidRecords.Any() && !result.InvalidRecords.Any();
            
            if (result.InvalidRecords.Any())
            {
                result.Errors.Add($"Found {result.InvalidRecords.Count} invalid records");
            }
            
            if (result.DuplicateRecords.Any())
            {
                result.Errors.Add($"Found {result.DuplicateRecords.Count} duplicate records");
            }

            return result;
        }

        private static List<string> ValidateRecord(ExcelImportDto record)
        {
            var validationErrors = new List<string>();

            if (record.SNo <= 0)
                validationErrors.Add("S No is required and must be greater than 0");

            if (string.IsNullOrWhiteSpace(record.RegNo))
                validationErrors.Add("Reg No is required");

            if (string.IsNullOrWhiteSpace(record.Name))
                validationErrors.Add("Name is required");

            if (string.IsNullOrWhiteSpace(record.Term))
                validationErrors.Add("Term is required");

            if (string.IsNullOrWhiteSpace(record.Level))
                validationErrors.Add("Level is required");

            if (string.IsNullOrWhiteSpace(record.Degree))
                validationErrors.Add("Degree is required");

            if (string.IsNullOrWhiteSpace(record.DegreeTitle))
                validationErrors.Add("Degree Title is required");

            if (record.DegreeSerialNo <= 0)
                validationErrors.Add("Degree Serial No is required and must be greater than 0");

            if (string.IsNullOrWhiteSpace(record.Conf))
                validationErrors.Add("Conf is required");

            if (string.IsNullOrWhiteSpace(record.Program))
                validationErrors.Add("Program is required");

            if (string.IsNullOrWhiteSpace(record.AttendedPeriod))
                validationErrors.Add("Attended Period is required");

            return validationErrors;
        }

        public async Task<int> SaveValidRecordsAsync(List<ExcelImportDto> validRecords, string createdBy)
        {
            if (!validRecords.Any()) return 0;

            var currentTime = DateTime.UtcNow;
            var savedCount = 0;

            // Process in batches for better performance
            for (int i = 0; i < validRecords.Count; i += BATCH_SIZE)
            {
                var batch = validRecords.Skip(i).Take(BATCH_SIZE).ToList();
                var entities = new List<ImportStudentsDegree>(batch.Count);

                // Convert batch to entities in parallel
                var convertedEntities = batch.AsParallel()
                    .WithDegreeOfParallelism(MAX_PARALLEL_TASKS)
                    .Select(record => new ImportStudentsDegree
                    {
                        UserSecretId = Guid.NewGuid().ToString(),
                        SNo = record.SNo,
                        RegNo = record.RegNo,
                        DegreeSerialNo = record.DegreeSerialNo,
                        Name = record.Name,
                        Program = record.Program,
                        Degree = record.Degree,
                        DegreeTitle = record.DegreeTitle,
                        Level = record.Level,
                        AttendedPeriod = record.AttendedPeriod,
                        Conf = record.Conf,
                        Term = record.Term,
                        CreatedBy = createdBy,
                        CreatedDate = currentTime,
                        ProgramDiscipline = string.Empty,
                        IsVerified = false,
                        IsPrint = false,
                        IsDeleted = false,
                        IsDuplicate = false,
                        Approved = false
                    }).ToList();

                // TODO: Replace with actual bulk database insert
                // Save entities to database
                await _dbContext.ImportStudentsDegrees.AddRangeAsync(convertedEntities);
                await _dbContext.SaveChangesAsync();
                
                // For SQL Server, consider using SqlBulkCopy for maximum performance:
                // await BulkInsertAsync(convertedEntities);
                
                savedCount += convertedEntities.Count;
                
                // Optional: Add progress reporting
                // await ReportProgress(i + batch.Count, validRecords.Count);
            }

            return savedCount;
        }

        // Optional: Implement bulk insert using SqlBulkCopy for maximum performance
        // private async Task BulkInsertAsync(List<ImportStudentsDegree> entities)
        // {
        //     var dataTable = ConvertToDataTable(entities);
        //     using var connection = new SqlConnection(_connectionString);
        //     using var bulkCopy = new SqlBulkCopy(connection)
        //     {
        //         DestinationTableName = "ImportStudentsDegree",
        //         BatchSize = BATCH_SIZE,
        //         BulkCopyTimeout = 300 // 5 minutes
        //     };
        //     
        //     await connection.OpenAsync();
        //     await bulkCopy.WriteToServerAsync(dataTable);
        // }
    }
}
