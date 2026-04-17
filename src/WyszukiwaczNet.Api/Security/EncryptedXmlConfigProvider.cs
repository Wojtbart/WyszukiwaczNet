using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;

namespace WyszukiwaczNet.Api.Security;

public class EncryptedXmlConfigSource : IConfigurationSource
{
    public string FilePath { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new EncryptedXmlConfigProvider(FilePath, Password);
}

public class EncryptedXmlConfigProvider : ConfigurationProvider
{
    private readonly string _filePath;
    private readonly string _password;

    public EncryptedXmlConfigProvider(string filePath, string password)
    {
        _filePath = filePath;
        _password = password;
    }

    public override void Load()
    {
        if (!File.Exists(_filePath)) return;

        try
        {
            var cipherBytes = File.ReadAllBytes(_filePath);
            var xml         = Decrypt(cipherBytes, _password);
            var doc         = XDocument.Parse(xml);

            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            ParseElement(doc.Root!, string.Empty, data);
            Data = data;
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                $"Nie uda³o siê odszyfrowaæ '{_filePath}'. SprawdŸ zmienn¹ œrodowiskow¹ CONFIG_KEY.");
        }
    }

    private static void ParseElement(XElement element, string prefix, Dictionary<string, string?> data)
    {
        if (!element.HasElements)
        {
            if (!string.IsNullOrEmpty(prefix))
                data[prefix] = element.Value;
            return;
        }

        foreach (var child in element.Elements())
        {
            var key = string.IsNullOrEmpty(prefix) ? child.Name.LocalName : $"{prefix}:{child.Name.LocalName}";
            ParseElement(child, key, data);
        }
    }

    private static string Decrypt(byte[] data, string password)
    {
        var salt       = data[..16];
        var iv         = data[16..32];
        var ciphertext = data[32..];

        using var kdf = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        var key = kdf.GetBytes(32);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV  = iv;

        using var ms   = new MemoryStream(ciphertext);
        using var cs   = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var out_ = new MemoryStream();
        cs.CopyTo(out_);
        return Encoding.UTF8.GetString(out_.ToArray());
    }
}

public static class EncryptedXmlConfigExtensions
{
    public static IConfigurationBuilder AddEncryptedXmlFile(
        this IConfigurationBuilder builder, string path, string password)
    {
        return builder.Add(new EncryptedXmlConfigSource { FilePath = path, Password = password });
    }
}
