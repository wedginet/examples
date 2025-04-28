public class LookupSpeciesService : ILookupSpeciesService
{
    private readonly HttpClient _http;

    public LookupSpeciesService(HttpClient http)
        => _http = http;

    public async Task<List<SpeciesDto>> GetAllAsync()
        => await _http.GetFromJsonAsync<List<SpeciesDto>>("api/lookupSpecies");

    public async Task<List<SpeciesDto>> SearchByNameAsync(string name)
        => await _http.GetFromJsonAsync<List<SpeciesDto>>($"api/lookupSpecies?name={Uri.EscapeDataString(name)}");

    public async Task<List<SpeciesDto>> GetChangedSinceAsync(DateTime since)
        => await _http.GetFromJsonAsync<List<SpeciesDto>>($"api/lookupSpecies/changed?lastUpdatedOn={since:o}");

    public async Task<SpeciesDto> CreateAsync(SpeciesDto dto)
    {
        var resp = await _http.PostAsJsonAsync("api/lookupSpecies", dto);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SpeciesDto>()!;
    }

    public async Task<bool> UpdateAsync(SpeciesDto dto)
    {
        var resp = await _http.PutAsJsonAsync($"api/lookupSpecies/{dto.Id}", dto);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var resp = await _http.DeleteAsync($"api/lookupSpecies/{id}");
        return resp.IsSuccessStatusCode;
    }

    public async Task<BulkInsertResult> BulkInsertAsync(IEnumerable<SpeciesDto> dtos)
    {
        var resp = await _http.PostAsJsonAsync("api/lookupSpecies/bulk", dtos);
        return await resp.Content.ReadFromJsonAsync<BulkInsertResult>()!;
    }
}
