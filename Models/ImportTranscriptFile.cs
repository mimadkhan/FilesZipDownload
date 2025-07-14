using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FilesZipDownload.Models
{
    public class ImportTranscriptFile
    {
        [Key]
        public string UserTransciptId { get; set; }
        public string RegistrationNo { get; set; }
        public string Name { get; set; }
        public string Degree { get; set; }
        public string Specialization { get; set; }
        public string DateAwarded { get; set; }
        public string Program { get; set; }  // BS, MS, PHD
        public string FilePath { get; set; }
        public string CreatedBy { get; set; }
        public bool IsVerified { get; set; }
        public bool IsPrint { get; set; }
        public bool IsDeleted { get; set; }
        public string Approved { get; set; }
    }

}
