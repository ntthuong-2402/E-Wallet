namespace Friday.Modules.Customer.Domain.Customers;

public sealed record CitizenDocument(
    CitizenDocumentType DocumentType,
    string IssuingCountryCode,
    string Number,
    string MaskedValue
)
{
    public static CitizenDocument Create(
        CitizenDocumentType documentType,
        string issuingCountryCode,
        string number
    )
    {
        if (!Enum.IsDefined(documentType))
            throw new ArgumentOutOfRangeException(nameof(documentType));

        string country = (issuingCountryCode ?? string.Empty).Trim().ToUpperInvariant();
        if (country.Length != 2 || country.Any(x => x is < 'A' or > 'Z'))
            throw new ArgumentException("Issuing country must be an ISO alpha-2 code.", nameof(issuingCountryCode));

        string normalized = NormalizeNumber(number);
        switch (documentType)
        {
            case CitizenDocumentType.VietnamCitizenId when country != "VN":
                throw new ArgumentException("Vietnam Citizen ID must use issuing country VN.", nameof(issuingCountryCode));
            case CitizenDocumentType.VietnamCitizenId when normalized.Length != 12 || !normalized.All(IsAsciiDigit):
                throw new ArgumentException("Vietnam Citizen ID must contain exactly 12 digits.", nameof(number));
            case CitizenDocumentType.Passport when normalized.Length is < 6 or > 20 || !normalized.All(IsAsciiLetterOrDigit):
                throw new ArgumentException("Passport must contain 6 to 20 letters or digits.", nameof(number));
        }

        return new CitizenDocument(documentType, country, normalized, Mask(normalized));
    }

    private static string NormalizeNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Citizen document number is required.", nameof(value));
        if (value.Any(x => !IsAsciiLetterOrDigit(x) && x is not (' ' or '-')))
            throw new ArgumentException("Citizen document contains unsupported characters.", nameof(value));
        return new string(value.Trim().Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static string Mask(string value)
    {
        int visible = Math.Min(4, value.Length);
        return new string('*', value.Length - visible) + value[^visible..];
    }
}
