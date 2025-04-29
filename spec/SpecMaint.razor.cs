using Microsoft.AspNetCore.Components;
using LookupAdminApp.Models;
using LookupAdminApp.Services;

namespace LookupAdminApp.Pages
{
    public partial class SpeciesMaintenance : ComponentBase
    {
        [Inject] public ILookupSpeciesService Service { get; set; } = null!;

        private List<SpeciesDto> items = new();
        private string           searchText = "";
        private bool             dialogVisible;
        private SpeciesDto       currentItem = new();

        protected override async Task OnInitializedAsync()
            => items = await Service.GetAllAsync();

        private async Task OnSearch()
        {
            items = string.IsNullOrWhiteSpace(searchText)
                ? await Service.GetAllAsync()
                : await Service.SearchByNameAsync(searchText);
        }

        private void ShowDialog(SpeciesDto dto)
        {
            // clone so edits don’t immediately reflect in the table
            currentItem = new SpeciesDto {
                Id              = dto.Id,
                Name            = dto.Name,
                EffectiveDate   = dto.EffectiveDate,
                ExpirationDate  = dto.ExpirationDate
            };
            dialogVisible = true;
        }

        private async Task SaveSpecies(SpeciesDto dto)
        {
            if (dto.Id == 0)
                await Service.CreateAsync(dto);
            else
                await Service.UpdateAsync(dto);

            items = await Service.GetAllAsync();
        }

        private async Task DeleteSpecies(int id)
        {
            if (await Service.DeleteAsync(id))
                items = await Service.GetAllAsync();
        }

        private async Task BulkAddSpecies(List<SpeciesDto> list)
        {
            _ = await Service.BulkInsertAsync(list);
            items = await Service.GetAllAsync();
        }
    }
}
