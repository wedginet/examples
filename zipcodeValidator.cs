using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

public class ZipCodeValidationAttribute : ValidationAttribute
{
    private static readonly Regex UsZipCodeRegex = new Regex(@"^\d{5}$");
    private static readonly Regex CaPostalCodeRegex = new Regex(@"^[A-Za-z]\d[A-Za-z]\s?\d[A-Za-z]\d$");

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrEmpty(value.ToString()))
        {
            return ValidationResult.Success; // Optional: Treat empty input as valid
        }

        string input = value.ToString();

        // Validate against both USA and Canadian formats
        if (UsZipCodeRegex.IsMatch(input) || CaPostalCodeRegex.IsMatch(input))
        {
            return ValidationResult.Success;
        }

        return new ValidationResult("Please enter a valid USA ZIP code or Canadian postal code.");
    }
}
