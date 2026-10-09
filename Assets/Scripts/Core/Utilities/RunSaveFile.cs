using System.IO;
using System.Text;

// Leitura e escrita do arquivo de save. A escrita vai pra um arquivo temporario e so depois substitui o original,
// entao o app morrer no meio da gravacao deixa o save anterior intacto.
public static class RunSaveFile
{
    private const string TempSuffix = ".tmp";

    public static bool Exists(string path)
    {
        return File.Exists(path);
    }

    public static string Read(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
    }

    public static void Write(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) == false)
            Directory.CreateDirectory(directory);

        var tempPath = path + TempSuffix;
        var bytes = Encoding.UTF8.GetBytes(contents);
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        if (File.Exists(path))
            File.Replace(tempPath, path, null);
        else
            File.Move(tempPath, path);
    }

    public static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        if (File.Exists(path + TempSuffix))
            File.Delete(path + TempSuffix);
    }
}
