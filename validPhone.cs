using System.Text.RegularExpressions;

public bool IsValidPhoneNumber(string candidate)
{
    if (string.IsNullOrEmpty(candidate))
    {
        return false;
    }

    // Regular expression pattern to match phone numbers
    string pattern = @"^(\+?[0-9]{1,4})?[\s\-\.]?\(?([0-9]{1,3})?\)?[\s\-\.]?[0-9]{1,4}[\s\-\.]?[0-9]{1,4}[\s\-\.]?[0-9]{1,9}(\s?(ext|x|extension)\s?[0-9]{1,5})?$";

    // Use Regex to match the phone number with the pattern
    Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

    return regex.IsMatch(candidate);
}
