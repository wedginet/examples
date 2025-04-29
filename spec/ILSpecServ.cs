
    public interface ILookupSpeciesService
    {
        Task<List<SpeciesDto>> GetAllAsync();
        Task<List<SpeciesDto>> SearchByNameAsync(string name);
        Task<List<SpeciesDto>> GetChangedSinceAsync(DateTime since);
        Task<SpeciesDto>       CreateAsync(SpeciesDto dto);
        Task<bool>             UpdateAsync(SpeciesDto dto);
        Task<bool>             DeleteAsync(int id);
        Task<BulkInsertResult> BulkInsertAsync(IEnumerable<SpeciesDto> dtos);
    }

