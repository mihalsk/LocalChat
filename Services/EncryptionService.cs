using System.Security.Cryptography;
using System.Text;

namespace LocalChat.Services;

public class EncryptionService
{
    private readonly byte[] _salt = Encoding.UTF8.GetBytes("LocalChatSALT2026");
    private readonly int _iterations = 10000;
    private readonly int _keySize = 256;

    public (byte[] Key, byte[] IV) DeriveKeyAndIV(string password)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(password, _salt, _iterations, HashAlgorithmName.SHA256);
        var key = deriveBytes.GetBytes(32);
        var iv = deriveBytes.GetBytes(16);
        return (key, iv);
    }

    public string Encrypt(string plainText, string password)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        var (key, iv) = DeriveKeyAndIV(password);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    public string Decrypt(string cipherText, string password)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;
        var (key, iv) = DeriveKeyAndIV(password);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var cipherBytes = Convert.FromBase64String(cipherText);
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}