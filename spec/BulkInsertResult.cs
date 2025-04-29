namespace LookupAdminApp.Models
{
    public class BulkInsertResult
    {
        public int TotalCount { get; set; }
        public int SucceededCount { get; set; }
        public int FailedCount => TotalCount - SucceededCount;
        public List<string> ErrorMessages { get; set; } = new();
    }
}
