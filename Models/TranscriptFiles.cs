using System.ComponentModel.DataAnnotations;

namespace FilesZipDownload.Models
{
    public class TranscriptFiles
    {
        [Key]
        public int FileId { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] FileContent { get; set; }
        public string FilePath { get; set; }
        public string UserIdFk { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
