using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServicePortal.Application.Abstractions;
using ServicePortal.Application.ServiceRequests.Queries;
using ServicePortal.Domain.ServiceRequests;
using ServicePortal.Infrastructure.Persistence;

namespace ServicePortal.Infrastructure.Tests;

public sealed class ServiceRequestRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"service-portal-tests-{Guid.NewGuid():N}");

    private string DataFile => Path.Combine(_directory, "data", "requests.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task EmptyStore_IsSeededWithSampleData_WhenEnabled()
    {
        using var repository = CreateRepository(seed: true, file: false);

        var counts = await repository.CountByStatusAsync(default);

        Assert.True(counts.Values.Sum() >= 10);
    }

    [Fact]
    public async Task FileStore_SurvivesRestart()
    {
        int id;
        using (var first = CreateRepository(seed: false, file: true))
        {
            id = await first.NextIdAsync(default);
            await first.AddAsync(NewRequest(id), default);
        }

        using var second = CreateRepository(seed: false, file: true);
        var reloaded = await second.FindAsync(id, default);

        Assert.NotNull(reloaded);
        Assert.Equal("Keyboard trap in signing modal", reloaded.Subject);
        Assert.Equal(id + 1, await second.NextIdAsync(default));
    }

    [Fact]
    public async Task FileStore_LeavesNoTemporaryFilesBehind()
    {
        using var repository = CreateRepository(seed: true, file: true);
        await repository.CountByStatusAsync(default);

        var files = Directory.GetFiles(Path.GetDirectoryName(DataFile)!);

        Assert.Equal(new[] { DataFile }, files);
    }

    [Fact]
    public async Task CorruptFile_FailsLoudly_AndIsNotOverwritten()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DataFile)!);
        await File.WriteAllTextAsync(DataFile, "{ not json");
        using var repository = CreateRepository(seed: true, file: true);

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.CountByStatusAsync(default));
        Assert.Equal("{ not json", await File.ReadAllTextAsync(DataFile));
    }

    [Fact]
    public async Task Update_WithStaleVersion_IsRejected()
    {
        using var repository = CreateRepository(seed: false, file: false);
        var request = NewRequest(await repository.NextIdAsync(default));
        await repository.AddAsync(request, default);

        var first = (await repository.FindAsync(request.Id, default))!;
        var second = (await repository.FindAsync(request.Id, default))!;
        first.ChangeStatus(RequestStatus.InProgress, Now);
        second.ChangeStatus(RequestStatus.Cancelled, Now);

        Assert.Equal(WriteOutcome.Succeeded, await repository.UpdateAsync(first, expectedVersion: 1, default));
        Assert.Equal(WriteOutcome.VersionMismatch, await repository.UpdateAsync(second, expectedVersion: 1, default));
        Assert.Equal(RequestStatus.InProgress, (await repository.FindAsync(request.Id, default))!.Status);
    }

    [Fact]
    public async Task ReturnedAggregates_AreCopies_SoCallersCannotMutateStoredState()
    {
        using var repository = CreateRepository(seed: false, file: false);
        var request = NewRequest(await repository.NextIdAsync(default));
        await repository.AddAsync(request, default);

        var copy = (await repository.FindAsync(request.Id, default))!;
        copy.ChangeStatus(RequestStatus.InProgress, Now);

        Assert.Equal(RequestStatus.Open, (await repository.FindAsync(request.Id, default))!.Status);
    }

    [Fact]
    public async Task Query_PagesWithoutDuplicatesOrGaps()
    {
        using var repository = CreateRepository(seed: true, file: false);
        var seen = new List<int>();

        for (var page = 1; ; page++)
        {
            var result = await repository.QueryAsync(
                ServiceRequestQuery.Default with { Sort = SortField.Priority, PageSize = 5, Page = page }, default);
            seen.AddRange(result.Items.Select(i => i.Id));
            if (!result.HasNextPage)
            {
                Assert.Equal(result.TotalCount, seen.Count);
                break;
            }
        }

        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public async Task Query_SearchMatchesReferenceSubjectAndName_CaseInsensitively()
    {
        using var repository = CreateRepository(seed: false, file: false);
        var request = NewRequest(await repository.NextIdAsync(default));
        await repository.AddAsync(request, default);

        foreach (var term in new[] { request.Reference.ToLowerInvariant(), "KEYBOARD TRAP", "alex" })
        {
            var result = await repository.QueryAsync(ServiceRequestQuery.Default with { Search = term }, default);
            Assert.Equal(request.Id, Assert.Single(result.Items).Id);
        }
    }

    private ServiceRequestRepository CreateRepository(bool seed, bool file)
    {
        ISnapshotStore store = file
            ? new JsonFileSnapshotStore(DataFile, NullLogger<JsonFileSnapshotStore>.Instance)
            : new InMemorySnapshotStore();
        var options = Options.Create(new StorageOptions { SeedSampleData = seed });
        return new ServiceRequestRepository(store, options, TimeProvider.System);
    }

    private static ServiceRequest NewRequest(int id) => ServiceRequest.Submit(
        id,
        "Keyboard trap in signing modal",
        "Focus cannot leave the signing modal with Tab or Escape.",
        new ContactDetails("Alex Morgan", "alex@example.com", null),
        "Accessibility barrier",
        Priority.High,
        Now);
}
