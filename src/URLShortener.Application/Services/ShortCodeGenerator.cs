using System.Security.Cryptography;
using URLShortener.Application.Interfaces;

namespace URLShortener.Application.Services;

public class ShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int CodeLength = 7;

    public string Generate()
    {
        Span<byte> bytes = stackalloc byte[CodeLength];
        RandomNumberGenerator.Fill(bytes);

        var chars = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
        {
            chars[i] = Alphabet[bytes[i] % 62];
        }

        return new string(chars);
    }
}
