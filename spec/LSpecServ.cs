using LookupAdminApp.Models;
using System.Net.Http.Json;

namespace LookupAdminApp.Services
{
    public class LookupSpeciesService : ILookupSpeciesService
    {
        private readonly HttpClient _http;

        public LookupSpeciesService(HttpClient http) => _http = http;

        public Task<List<SpeciesDto>> GetAllAsync()
            => _http.GetFromJsonAsync<List<SpeciesDto>>("api/lookupSpecies")!;

        public Task<List<SpeciesDto>> SearchByNameAsync(string name)
            => _http.GetFromJsonAsync<List<SpeciesDto>>($"api/lookupSpecies?name={Uri.EscapeDataString(name)}")!;

        public Task<List<SpeciesDto>> GetChangedSinceAsync(DateTime since)
            => _http.GetFromJsonAsync<List<SpeciesDto>>($"api/lookupSpecies/changed?lastUpdatedOn={since:o}")!;

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
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<BulkInsertResult>()!;
        }
    }
}
