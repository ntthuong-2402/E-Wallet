using System.Security.Cryptography;
using Friday.Modules.Customer.Application.Customers;

namespace Friday.Modules.Customer.Infrastructure.Security;

public sealed class SecureCustomerCodeGenerator : ICustomerCodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public string Generate()
    {
        byte[] random = RandomNumberGenerator.GetBytes(10);
        char[] encoded = new char[16];
        int output = 0;
        int buffer = 0;
        int bits = 0;

        foreach (byte value in random)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                encoded[output++] = Alphabet[(buffer >> bits) & 31];
            }
        }

        return $"CUS_{new string(encoded)}";
    }
}
