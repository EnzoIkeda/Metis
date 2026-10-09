using System.IO;

namespace Metis.Core.Tests;

public class RunSaveFileTests
{
    private string _directory;
    private string _path;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "metis-save-" + System.Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "run_save.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Test]
    public void Write_CreatesDirectoryAndRoundTrips()
    {
        RunSaveFile.Write(_path, "{\"a\":1}");

        Assert.That(RunSaveFile.Exists(_path), Is.True);
        Assert.That(RunSaveFile.Read(_path), Is.EqualTo("{\"a\":1}"));
    }

    [Test]
    public void Write_OverExistingFile_ReplacesItAndLeavesNoTemporaryFile()
    {
        RunSaveFile.Write(_path, "primeiro");
        RunSaveFile.Write(_path, "segundo");

        Assert.That(RunSaveFile.Read(_path), Is.EqualTo("segundo"));
        Assert.That(File.Exists(_path + ".tmp"), Is.False);
    }

    // Um temporario largado por uma gravacao interrompida nao pode estragar o save anterior nem a proxima gravacao.
    [Test]
    public void LeftoverTemporaryFile_DoesNotAffectReadOrNextWrite()
    {
        RunSaveFile.Write(_path, "valido");
        File.WriteAllText(_path + ".tmp", "meio-grava");

        Assert.That(RunSaveFile.Read(_path), Is.EqualTo("valido"));

        RunSaveFile.Write(_path, "novo");
        Assert.That(RunSaveFile.Read(_path), Is.EqualTo("novo"));
    }

    [Test]
    public void Read_MissingFile_ReturnsNull()
    {
        Assert.That(RunSaveFile.Read(_path), Is.Null);
    }

    [Test]
    public void Delete_RemovesFileAndTemporary()
    {
        RunSaveFile.Write(_path, "x");
        File.WriteAllText(_path + ".tmp", "y");

        RunSaveFile.Delete(_path);

        Assert.That(File.Exists(_path), Is.False);
        Assert.That(File.Exists(_path + ".tmp"), Is.False);
    }
}
