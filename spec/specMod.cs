public class SpeciesDto
{
    public int    Id             { get; set; }
    public string Name           { get; set; } = "";
    public DateTime EffectiveDate{ get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime LastUpdatedOn{ get; set; }
}
