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
        var userData = await _dbContext.ImportTransciptFiles.ToListAsync();
        var fileData = await _dbContext.TranscriptFiles
                        .Where(f => f.ContentType == "application/pdf" && f.FileContent != null)
                        .ToListAsync();

        // Map user info for lookup
        var userLookup = userData.ToDictionary(u => u.UserTransciptId, u => u);

        using var memoryStream = new MemoryStream();
        using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in fileData)
            {
                if (!userLookup.TryGetValue(file.UserIdFk, out var user))
                    continue; // Skip if user metadata is missing

                string program = string.IsNullOrWhiteSpace(user.Program) ? "UnknownProgram" : user.Program.ToUpper();
                string department = string.IsNullOrWhiteSpace(user.Degree) ? "UnknownDept" : user.Degree.ToUpper();
                string regNo = string.IsNullOrWhiteSpace(user.RegistrationNo) ? "NoReg" : user.RegistrationNo;

                // Clean filename from invalid characters
                string safeName = Regex.Replace($"{regNo}", @"[^a-zA-Z0-9_\- ]+", "_");
                string zipPath = Path.Combine("UserTranscript", program, department, $"{safeName}.pdf");

                var entry = zip.CreateEntry(zipPath, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                using var fileStream = new MemoryStream(file.FileContent);
                if (fileStream.Length > 0)
                {
                    await fileStream.CopyToAsync(entryStream);
                }
            }
        }

        memoryStream.Position = 0;
        return File(memoryStream.ToArray(), "application/zip", "StructuredUserTranscripts.zip");
    }

}

