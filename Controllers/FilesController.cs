using FilesZipDownload.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.RegularExpressions;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public FilesController(AppDbContext context)
    {
        _dbContext = context;
    }

    [HttpGet("download-all-zip")]
    public async Task<IActionResult> DownloadStructuredZip()
    {
        return await DownloadFilteredZip(null, null, null);
    }
    
    [HttpGet("download-filtered-zip")]
    public async Task<IActionResult> DownloadFilteredZip(
        [FromQuery] string program = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null)
    {
        try
        {
            // Instead of using the model directly, let's use a more basic approach to avoid type casting issues
            var userTranscriptIds = await _dbContext.ImportTransciptFiles
                .Where(u => u.UserTransciptId != null)
                .Select(u => new
                {
                    u.UserTransciptId,
                    u.RegistrationNo,
                    u.Name,
                    u.Degree,
                    u.Program
                })
                .ToListAsync();
                
            // Convert to our model manually to avoid casting issues
            var userData = userTranscriptIds.Select(u => new ImportTranscriptFile
            {
                UserTransciptId = u.UserTransciptId,
                RegistrationNo = u.RegistrationNo ?? "",
                Name = u.Name ?? "",
                Degree = u.Degree ?? "",
                Program = u.Program?.ToString() ?? "" // Ensure it's a string
            }).ToList();
            
            // Filter by program if specified
            if (!string.IsNullOrWhiteSpace(program) && program.ToLower() != "all")
            {
                userData = userData.Where(u => 
                    !string.IsNullOrWhiteSpace(u.Program) && 
                    u.Program.ToLower() == program.ToLower())
                    .ToList();
            }
        
            // Get file data with optional time frame filter
            var fileDataQuery = _dbContext.TranscriptFiles
                            .Where(f => f.ContentType == "application/pdf" && f.FileContent != null);
            
            if (year.HasValue)
            {
                fileDataQuery = fileDataQuery.Where(f => f.CreatedDate.Year == year.Value);
            }
            
            if (month.HasValue && month.Value >= 1 && month.Value <= 12)
            {
                fileDataQuery = fileDataQuery.Where(f => f.CreatedDate.Month == month.Value);
            }
            
            // Use a projection to avoid casting issues
            var fileDataResults = await fileDataQuery
                .Select(f => new
                {
                    f.FileId,
                    f.FileName,
                    f.ContentType,
                    f.FileContent,
                    f.UserIdFk
                })
                .ToListAsync();

            // Map user info for lookup
            var userLookup = userData.ToDictionary(u => u.UserTransciptId, u => u);

            // Create the zip file
            using var memoryStream = new MemoryStream();
            using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                foreach (var file in fileDataResults)
                {
                    // Skip files with null UserIdFk
                    if (string.IsNullOrEmpty(file.UserIdFk))
                        continue;
                        
                    if (!userLookup.TryGetValue(file.UserIdFk, out var user))
                        continue; // Skip if user metadata is missing

                    string programName = string.IsNullOrWhiteSpace(user.Program) ? "UnknownProgram" : user.Program.ToUpper();
                    string department = string.IsNullOrWhiteSpace(user.Degree) ? "UnknownDept" : user.Degree.ToUpper();
                    string regNo = string.IsNullOrWhiteSpace(user.RegistrationNo) ? "NoReg" : user.RegistrationNo;

                    // Clean filename from invalid characters
                    string safeName = Regex.Replace($"{regNo}", @"[^a-zA-Z0-9_\- ]+", "_");
                    string zipPath = Path.Combine("UserTranscript", programName, department, $"{safeName}.pdf");

                    // Skip files with null content
                    if (file.FileContent == null)
                        continue;
                        
                    var entry = zip.CreateEntry(zipPath, CompressionLevel.Fastest);
                    using var entryStream = entry.Open();
                    using var fileStream = new MemoryStream(file.FileContent);
                    if (fileStream.Length > 0)
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                    user.IsPrint = true; // Mark as printed
                }
            }

            // ✅ Bulk update all changed users
            _dbContext.ImportTransciptFiles.UpdateRange(userLookup.Values);
            await _dbContext.SaveChangesAsync();
            
            return File(memoryStream.ToArray(), "application/zip", GenerateZipFileName(program, year, month));
        }
        catch (Exception ex)
        {
            // Log the error
            Console.WriteLine($"Error in DownloadFilteredZip: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Return a meaningful error response
            return StatusCode(500, $"An error occurred while processing your request: {ex.Message}");
        }
    }
    
    [HttpGet("available-programs")]
    public async Task<IActionResult> GetAvailablePrograms()
    {
        try
        {
            // Use projection to avoid casting issues
            var programResults = await _dbContext.ImportTransciptFiles
                .Where(u => u.Program != null)
                .Select(u => new { ProgramName = u.Program })
                .ToListAsync();
                
            // Process the results manually to avoid casting issues
            var programs = programResults
                .Where(p => !string.IsNullOrWhiteSpace(p.ProgramName?.ToString()))
                .Select(p => p.ProgramName.ToString().ToUpper())
                .Distinct()
                .OrderBy(p => p)
                .ToList();
                
            return Ok(programs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetAvailablePrograms: {ex.Message}");
            return StatusCode(500, $"An error occurred while retrieving programs: {ex.Message}");
        }
    }
    
    [HttpGet("available-years")]
    public async Task<IActionResult> GetAvailableYears()
    {
        try
        {
            // Use projection to avoid casting issues
            var dateResults = await _dbContext.TranscriptFiles
                .Select(f => new { Year = f.CreatedDate.Year })
                .ToListAsync();
                
            var years = dateResults
                .Select(d => d.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();
                
            return Ok(years);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetAvailableYears: {ex.Message}");
            return StatusCode(500, $"An error occurred while retrieving years: {ex.Message}");
        }
    }
    
    [HttpGet("available-months")]
    public async Task<IActionResult> GetAvailableMonths([FromQuery] int? year = null)
    {
        try
        {
            // Use projection to avoid casting issues
            var query = _dbContext.TranscriptFiles.AsQueryable();
            
            if (year.HasValue)
            {
                query = query.Where(f => f.CreatedDate.Year == year.Value);
            }
            
            var dateResults = await query
                .Select(f => new { Month = f.CreatedDate.Month })
                .ToListAsync();
                
            var months = dateResults
                .Select(d => d.Month)
                .Distinct()
                .OrderBy(m => m)
                .ToList();
                
            // Convert month numbers to month names
            var monthNames = months.Select(m => new { 
                MonthNumber = m, 
                MonthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m) 
            }).ToList();
                
            return Ok(monthNames);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetAvailableMonths: {ex.Message}");
            return StatusCode(500, $"An error occurred while retrieving months: {ex.Message}");
        }
    }
    
    private string GenerateZipFileName(string program, int? year, int? month)
    {
        var parts = new List<string> { "UserTranscripts" };
        
        if (!string.IsNullOrWhiteSpace(program) && program.ToLower() != "all")
        {
            parts.Add(program.ToUpper());
        }
        
        if (year.HasValue)
        {
            parts.Add(year.Value.ToString());
            
            if (month.HasValue && month.Value >= 1 && month.Value <= 12)
            {
                string monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Value);
                parts.Add(monthName);
            }
        }
        
        return string.Join("-", parts) + ".zip";
    }
}

