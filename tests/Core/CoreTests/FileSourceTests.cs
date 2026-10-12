using PublicTxt.Core.Instances;

namespace CoreTests;

public class InMemoryFileSourceTests
{
    private static readonly DateTimeOffset When = new(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static InMemoryFileSource Sample()
    {
        var source = new InMemoryFileSource();
        source.Add("wiki/index.md", "# Home", When);
        source.Add("wiki/recipes/bread.md", "# Bread");
        source.Add("wiki/.obsidian/workspace.md", "");
        source.Add("README.md", "readme");
        return source;
    }

    [Fact]
    public void Directories_ExistImplicitly_AndRootAlwaysExists()
    {
        var source = Sample();

        Assert.True(source.DirectoryExists(""));
        Assert.True(source.DirectoryExists("wiki"));
        Assert.True(source.DirectoryExists("wiki/recipes"));
        Assert.False(source.DirectoryExists("wik"));
        Assert.False(source.DirectoryExists("wiki/index.md"));
        Assert.True(new InMemoryFileSource().DirectoryExists(""));
    }

    [Fact]
    public void Enumerate_ReturnsImmediateChildrenOnly_Unfiltered()
    {
        var source = Sample();

        Assert.Equal(new[] { "wiki/index.md" }, source.EnumerateFiles("wiki"));
        Assert.Equal(new[] { "README.md" }, source.EnumerateFiles(""));
        Assert.Equal(new[] { "wiki/.obsidian", "wiki/recipes" }, source.EnumerateDirectories("wiki").Order());
        Assert.Equal(new[] { "wiki" }, source.EnumerateDirectories(""));
        Assert.Empty(source.EnumerateFiles("missing"));
        Assert.Empty(source.EnumerateDirectories("missing"));
    }

    [Fact]
    public void Paths_AreNormalised_AndCaseSensitive()
    {
        var source = Sample();

        Assert.True(source.FileExists(@"wiki\recipes\..\index.md"));
        Assert.True(source.FileExists("./wiki//index.md"));
        Assert.False(source.FileExists("WIKI/index.md"));
        Assert.Equal("# Home", source.ReadAllText("wiki/index.md"));
        Assert.Equal(When, source.GetLastModified("wiki/index.md"));
    }

    [Fact]
    public void Add_ReplacesExisting_AndRemoveDeletes()
    {
        var source = Sample();

        source.Add("wiki/index.md", "# New");
        Assert.Equal("# New", source.ReadAllText("wiki/index.md"));
        Assert.Equal(4, source.Count);

        Assert.True(source.Remove("wiki/index.md"));
        Assert.False(source.FileExists("wiki/index.md"));
        Assert.False(source.Remove("wiki/index.md"));
    }

    [Fact]
    public void MissingFile_Throws()
    {
        var source = Sample();

        Assert.Throws<FileNotFoundException>(() => source.ReadAllText("wiki/nope.md"));
        Assert.Throws<FileNotFoundException>(() => source.GetLastModified("wiki/nope.md"));
    }

    [Theory]
    [InlineData("../x.md")]
    [InlineData("wiki/../../x.md")]
    [InlineData("/etc/passwd")]
    public void EscapingPaths_Throw(string path)
    {
        var source = Sample();

        Assert.Throws<UnauthorizedAccessException>(() => source.FileExists(path));
        Assert.Throws<UnauthorizedAccessException>(() => source.Add(path, ""));
    }

    [Fact]
    public void Add_RejectsRootAsFilePath()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryFileSource().Add("./", "x"));
    }
}

public class FileSourceParityTests
{
    [Fact]
    public void DiskAndMemory_ProduceTheSameCatalog()
    {
        var disk = InstanceContentReader.Open(Fixtures.SampleInstance).BuildCatalog();
        var memory = InstanceContentReader.Open(Fixtures.LoadSampleInstanceIntoMemory()).BuildCatalog();

        Assert.Equal(Describe(disk.Items), Describe(memory.Items));
        Assert.Equal(disk.Tags, memory.Tags);
        Assert.Equal(
            disk.Links.Select(l => (l.Source.RelativePath, l.Link.Target, l.ResolvedPath, l.Exists)),
            memory.Links.Select(l => (l.Source.RelativePath, l.Link.Target, l.ResolvedPath, l.Exists)));
    }

    [Fact]
    public void InstanceDetection_AndSettings_WorkFromMemory()
    {
        var memory = Fixtures.LoadSampleInstanceIntoMemory();

        Assert.True(InstanceLayout.IsInstance(memory));
        Assert.False(InstanceLayout.IsInstance(new InMemoryFileSource()));
        Assert.Equal(new TxtInstanceSettingsStore().Load(Fixtures.SampleInstance).WikiPath,
            new TxtInstanceSettingsStore().Load(memory).WikiPath);
    }

    [Fact]
    public void InvalidSettings_InMemory_ReportsFileAndSource()
    {
        var source = new InMemoryFileSource();
        source.Add(InstanceLayout.SettingsFileRelativePath, "{ not json");

        var ex = Assert.Throws<InvalidOperationException>(() => new TxtInstanceSettingsStore().Load(source));
        Assert.Contains(InstanceLayout.SettingsFileRelativePath, ex.Message);
        Assert.Contains("in-memory", ex.Message);
    }

    private static IEnumerable<string> Describe(IEnumerable<PublicTxt.Core.Content.ContentItem> items) =>
        items.Select(i => $"{i.RelativePath}|{i.Type}|{i.Title}|{i.Date}|{i.ModifiedAt:O}|{string.Join(',', i.Tags)}|{i.Body.Length}");
}
