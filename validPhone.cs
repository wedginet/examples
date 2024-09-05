/*
    Example Valid Phone Numbers:
+1 (555) 123-4567
(555) 123-4567
555-1234
123-456-7890
+44 20 1234 5678 ext 123
+91 (80) 12345678 x123
123-4567 ext 12
*/
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
