using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace FilesZipDownload.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<TranscriptFiles> TranscriptFiles { get; set; }
        public DbSet<ImportTranscriptFile> ImportTransciptFiles { get; set; }
    }

}
