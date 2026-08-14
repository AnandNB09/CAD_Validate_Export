namespace CADValidator.Models
{
    public class ValidationResult
    {
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string RuleName { get; set; }
        public ValidationStatus Status { get; set; }
        public string Message { get; set; }

        public ValidationResult(string fileName, string fileType, string ruleName, ValidationStatus status, string message)
        {
            FileName = fileName;
            FileType = fileType;
            RuleName = ruleName;
            Status = status;
            Message = message;
        }
    }
}