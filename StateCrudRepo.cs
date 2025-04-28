// -----------------------------------------------------------------------------
// 1) A more complete repository with all CRUD methods
// -----------------------------------------------------------------------------
public class LookupSpeciesSqlRepository : ILookupSpeciesRepository
{
    private readonly IDbConnection _db;
    private readonly ILogger<LookupSpeciesSqlRepository> _log;
    // a “reasonable” floor for last‐updated
    private static readonly DateTime DefaultDate = new(2024, 1, 1);

    public LookupSpeciesSqlRepository(IDbConnection db, ILogger<LookupSpeciesSqlRepository> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<IEnumerable<LookupSpecies>> GetAllAsync()
    {
        const string sql = @"SELECT * FROM dbo.LookupSpecies WHERE DeletedOn IS NULL";
        return await _db.QueryAsync<LookupSpecies>(sql);
    }

    public async Task<IEnumerable<LookupSpecies>> GetByNameAsync(string name)
    {
        const string sql = @"
            SELECT * 
              FROM dbo.LookupSpecies 
             WHERE DeletedOn IS NULL
               AND Name LIKE @Pattern";
        return await _db.QueryAsync<LookupSpecies>(sql,
            new { Pattern = $"%{name}%" });
    }

    public async Task<IEnumerable<LookupSpecies>> GetChangedSinceAsync(DateTime? lastUpdatedOn)
    {
        var since = (lastUpdatedOn == null || lastUpdatedOn < DefaultDate)
                    ? DefaultDate
                    : lastUpdatedOn.Value;

        const string sql = @"
            SELECT * 
              FROM dbo.LookupSpecies 
             WHERE LastUpdatedOn >= @Since
               AND DeletedOn IS NULL";
        return await _db.QueryAsync<LookupSpecies>(sql,
            new { Since = since });
    }

    public async Task<int> InsertAsync(LookupSpecies s)
    {
        const string sql = @"
            INSERT INTO dbo.LookupSpecies
                (Name, EffectiveDate, ExpirationDate, LastUpdatedOn)
            OUTPUT INSERTED.Id
            VALUES
                (@Name, @EffectiveDate, @ExpirationDate, @LastUpdatedOn)";
        return await _db.ExecuteScalarAsync<int>(sql, s);
    }

    public async Task<bool> UpdateAsync(LookupSpecies s)
    {
        const string sql = @"
            UPDATE dbo.LookupSpecies
               SET Name           = @Name,
                   EffectiveDate  = @EffectiveDate,
                   ExpirationDate = @ExpirationDate,
                   LastUpdatedOn  = @LastUpdatedOn
             WHERE Id = @Id";
        var rows = await _db.ExecuteAsync(sql, s);
        return rows > 0;
    }

    /// <summary>
    /// “Soft” delete: mark DeletedOn and bump LastUpdatedOn.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        const string sql = @"
            UPDATE dbo.LookupSpecies
               SET DeletedOn    = SYSDATETIME(),
                   LastUpdatedOn = SYSDATETIME()
             WHERE Id = @Id";
        var rows = await _db.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }
}

// -----------------------------------------------------------------------------
// 2) API Controller wiring those methods up
// -----------------------------------------------------------------------------
[ApiController]
[Route("api/[controller]")]
public class LookupSpeciesController : ControllerBase
{
    private readonly ILookupSpeciesRepository _repo;

    public LookupSpeciesController(ILookupSpeciesRepository repo)
        => _repo = repo;

    /// <summary>Get all lookups, or filter by name.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? name)
    {
        var list = string.IsNullOrWhiteSpace(name)
            ? await _repo.GetAllAsync()
            : await _repo.GetByNameAsync(name);
        return Ok(list);
    }

    /// <summary>Get only those changed since the client’s last sync time.</summary>
    [HttpGet("changed")]
    public async Task<IActionResult> GetChanged([FromQuery] DateTime? lastUpdatedOn)
    {
        var list = await _repo.GetChangedSinceAsync(lastUpdatedOn);
        return Ok(list);
    }

    /// <summary>Get one by its primary key.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var item = (await _repo.GetByNameAsync(null))
                       .FirstOrDefault(x => x.Id == id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    /// <summary>Insert a new lookup entry.</summary>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] LookupSpecies s)
    {
        s.LastUpdatedOn = DateTime.UtcNow;
        var id = await _repo.InsertAsync(s);
        s.Id = id;
        return CreatedAtAction(nameof(Get), new { id }, s);
    }

    /// <summary>Update an existing entry.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Put(int id, [FromBody] LookupSpecies s)
    {
        s.Id = id;
        s.LastUpdatedOn = DateTime.UtcNow;
        var ok = await _repo.UpdateAsync(s);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>“Delete” (soft) by id.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _repo.DeleteAsync(id);
        if (!ok) return NotFound();
        return NoContent();
    }
}
