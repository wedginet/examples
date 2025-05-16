var list = CsvText
    // split into lines, toss any blank ones
    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
    // split each line into columns (trimming each entry)
    .Select(line => line.Split(',', StringSplitOptions.TrimEntries))
    // enforce at least 3 columns + mandatory Name + mandatory EffectiveDate
    .Where(parts =>
        parts.Length >= 3
        && !string.IsNullOrWhiteSpace(parts[1])                         // Name non-empty
        && DateTime.TryParse(parts[2], out _))                           // EffectiveDate parses
    .Select(parts =>
    {
        // parse parent
        string rawParent = parts[0];
        // if it literally says "NULL" (case-insensitive) or is empty, treat as null
        string? parentId = string.IsNullOrWhiteSpace(rawParent)
            || rawParent.Equals("NULL", StringComparison.OrdinalIgnoreCase)
            ? null
            : rawParent;

        // parse expiration
        DateTime? expiration = null;
        if (parts.Length > 3
            && !parts[3].Equals("NULL", StringComparison.OrdinalIgnoreCase)
            && DateTime.TryParse(parts[3], out var parsedExp))
        {
            expiration = parsedExp;
        }

        return new OperationTypeDto
        {
            // leave Id as null so EF/core will generate one on Insert
            Id             = null,
            ParentId       = parentId,
            Name           = parts[1],
            EffectiveDate  = DateTime.Parse(parts[2]),  // safe because we TryParse’d above
            ExpirationDate = expiration
        };
    })
    .ToList();
