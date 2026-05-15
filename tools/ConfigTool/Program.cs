using System.Security.Cryptography;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "ConfigTool — Encrypt / Decrypt";

while (true)
{
    Console.Clear();
    Console.WriteLine("=== ConfigTool ===");
    Console.WriteLine("1. Encrypt file");
    Console.WriteLine("2. Decrypt file (show content)");
    Console.WriteLine("3. Decrypt file (save to disk)");
    Console.WriteLine("4. Exit");
    Console.Write("\nChoice: ");

    var choice = Console.ReadLine()?.Trim();
    if (choice == "4") break;

    switch (choice)
    {
        case "1": EncryptFlow(); break;
        case "2": DecryptFlow(saveToDisk: false); break;
        case "3": DecryptFlow(saveToDisk: true); break;
        default:
            Console.WriteLine("Unknown option.");
            Pause();
            break;
    }
}

static void EncryptFlow()
{
    Console.Write("Input file path: ");
    var input = Console.ReadLine()?.Trim();
    if (!File.Exists(input)) { Console.WriteLine("File not found."); Pause(); return; }

    Console.Write("Output encrypted file path (.enc): ");
    var output = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(output)) output = input + ".enc";

    var password = ReadPassword("Password: ");
    var confirm  = ReadPassword("Confirm password: ");
    if (password != confirm) { Console.WriteLine("Passwords do not match."); Pause(); return; }

    try
    {
        var plainBytes = File.ReadAllBytes(input);
        var encrypted  = Encrypt(plainBytes, password);
        File.WriteAllBytes(output, encrypted);
        Console.WriteLine($"\nEncrypted => {output}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    Pause();
}

static void DecryptFlow(bool saveToDisk)
{
    Console.Write("Encrypted file path: ");
    var input = Console.ReadLine()?.Trim();
    if (!File.Exists(input)) { Console.WriteLine("File not found."); Pause(); return; }

    var password = ReadPassword("Password: ");

    try
    {
        var cipherBytes = File.ReadAllBytes(input);
        var plain       = Decrypt(cipherBytes, password);
        var text        = Encoding.UTF8.GetString(plain);

        if (saveToDisk)
        {
            var defaultOut = input.EndsWith(".enc") ? input[..^4] : input + ".dec";
            Console.Write($"Output file path [{defaultOut}]: ");
            var output = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(output)) output = defaultOut;
            File.WriteAllText(output, text, Encoding.UTF8);
            Console.WriteLine($"\nDecrypted => {output}");
        }
        else
        {
            Console.WriteLine("\n--- Decrypted content ---");
            Console.WriteLine(text);
            Console.WriteLine("-------------------------");
        }
    }
    catch (CryptographicException)
    {
        Console.WriteLine("Wrong password or corrupted file.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    Pause();
}

// File format: [16 salt][16 IV][ciphertext]
static byte[] Encrypt(byte[] plaintext, string password)
{
    var salt = RandomNumberGenerator.GetBytes(16);
    var iv   = RandomNumberGenerator.GetBytes(16);
    var key  = DeriveKey(password, salt);

    using var aes = Aes.Create();
    aes.Key = key;
    aes.IV  = iv;

    using var ms = new MemoryStream();
    ms.Write(salt);
    ms.Write(iv);
    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        cs.Write(plaintext);

    return ms.ToArray();
}

static byte[] Decrypt(byte[] data, string password)
{
    var salt       = data[..16];
    var iv         = data[16..32];
    var ciphertext = data[32..];
    var key        = DeriveKey(password, salt);

    using var aes = Aes.Create();
    aes.Key = key;
    aes.IV  = iv;

    using var ms  = new MemoryStream(ciphertext);
    using var cs  = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
    using var out_ = new MemoryStream();
    cs.CopyTo(out_);
    return out_.ToArray();
}

static byte[] DeriveKey(string password, byte[] salt)
{
    using var kdf = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
    return kdf.GetBytes(32);
}

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var sb = new StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
        {
            sb.Remove(sb.Length - 1, 1);
            Console.Write("\b \b");
        }
        else if (key.Key != ConsoleKey.Backspace)
        {
            sb.Append(key.KeyChar);
            Console.Write('*');
        }
    }
    Console.WriteLine();
    return sb.ToString();
}

static void Pause()
{
    Console.Write("\nPress any key...");
    Console.ReadKey(intercept: true);
}
