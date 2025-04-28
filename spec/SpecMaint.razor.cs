public partial class SpeciesMaintenance
{
    private List<SpeciesDto> items = new();
    private string           searchText = "";
    private bool             dialogVisible;
    private SpeciesDto       currentItem = new();

    protected override async Task OnInitializedAsync()
        => await LoadAll();

    private async Task LoadAll()
        => items = await Service.GetAllAsync();

    private async Task OnSearch()
    {
        items = string.IsNullOrWhiteSpace(searchText)
            ? await Service.GetAllAsync()
            : await Service.SearchByNameAsync(searchText);
    }

    private void OpenDialog(SpeciesDto sp)
    {
        currentItem = new SpeciesDto {
            Id             = sp.Id,
            Name           = sp.Name,
            EffectiveDate  = sp.EffectiveDate,
            ExpirationDate = sp.ExpirationDate
        };
        dialogVisible = true;
    }

    private async Task SaveSpecies(SpeciesDto sp)
    {
        if (sp.Id == 0)
            currentItem = await Service.CreateAsync(sp);
        else
            await Service.UpdateAsync(sp);

        await LoadAll();
    }

    private async Task DeleteSpecies(int id)
    {
        if (!await Service.DeleteAsync(id)) return;
        await LoadAll();
    }

    private async Task BulkAddSpecies(List<SpeciesDto> list)
    {
        var result = await Service.BulkInsertAsync(list);
        // you could show result.Succeeded vs. result.FailedCount…
        await LoadAll();
    }
}
