using System.ComponentModel.DataAnnotations;

namespace FilesZipDownload.Models
{
    public partial class ImportStudentsDegree
    {
        [Required]
        public string UserSecretId { get; set; } = string.Empty;
        
        [Required]
        public int SNo { get; set; }
        
        [Required]
        [StringLength(50)]
        public string RegNo { get; set; } = string.Empty;
        
        [Required]
        public int DegreeSerialNo { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string Program { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string Degree { get; set; } = string.Empty;
        
        [Required]
        [StringLength(200)]
        public string DegreeTitle { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string Level { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string AttendedPeriod { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string Conf { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string Term { get; set; } = string.Empty;
        
        public bool? IsVerified { get; set; }
        public bool? IsPrint { get; set; }
        
        [Required]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
        
        [Required]
        public DateTime CreatedDate { get; set; }
        
        [StringLength(100)]
        public string ProgramDiscipline { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string ModifiedBy { get; set; } = string.Empty;
        
        public DateTime? ModifiedDate { get; set; }
        public bool? IsDeleted { get; set; }
        public bool? IsDuplicate { get; set; }
        public bool? Approved { get; set; }
    }
}
