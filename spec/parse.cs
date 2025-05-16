private async Task ParseAndUpload()
{
    var list = CsvText
        // split into lines, remove blanks
        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
        // split each line into parts, trimming whitespace
        .Select(line => line.Split(',', StringSplitOptions.TrimEntries))
        // require at least 3 columns, non-empty Name, valid EffectiveDate
        .Where(parts =>
            parts.Length >= 3
            && !string.IsNullOrWhiteSpace(parts[1])
            && DateTime.TryParse(parts[2], out _))
        .Select(parts =>
        {
            // Id left null
            // ParentId: null if empty or "NULL"
            var rawParent = parts[0];
            string? parentId = string.IsNullOrWhiteSpace(rawParent)
                || rawParent.Equals("NULL", StringComparison.OrdinalIgnoreCase)
                ? null
                : rawParent;

            // ExpirationDate: parse only if present, non-NULL, and valid
            DateTime? expirationDate = null;
            if (parts.Length > 3
                && !string.IsNullOrWhiteSpace(parts[3])
                && !parts[3].Equals("NULL", StringComparison.OrdinalIgnoreCase)
                && DateTime.TryParse(parts[3], out var parsedExp))
            {
                expirationDate = parsedExp;
            }

            return new OperationTypeDto
            {
                Id              = null,
                ParentId        = parentId,
                Name            = parts[1],
                EffectiveDate   = DateTime.Parse(parts[2]),  // safe: we filtered with TryParse
                ExpirationDate  = expirationDate
            };
        })
        .ToList();

    await OnParsed.InvokeAsync(list);
}
