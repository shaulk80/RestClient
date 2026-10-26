using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.Environments;
using RestClient.Application.Secrets;
using RestClient.Application.Workspaces;
using RestClient.Domain.Environments;
using RestClient.Domain.Results;
using RestClient.Application.Tests.Secrets;

namespace RestClient.Application.Tests.Environments;

[TestFixture]
public class EnvironmentServiceTests
{
    private static readonly Guid NewId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private EnvironmentRepositoryFake _repository = null!;
    private InMemorySettingsStore _settings = null!;
    private ISecretService _secrets = null!;
    private EnvironmentService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new EnvironmentRepositoryFake();
        _settings = new InMemorySettingsStore();
        _secrets = A.Fake<ISecretService>();
        A.CallTo(() => _secrets.Encrypt(A<string>._))
            .ReturnsLazily((string plain) => Result<string>.Success("enc:" + plain));
        var ids = A.Fake<IIdGenerator>();
        A.CallTo(() => ids.NewId()).Returns(NewId);
        var workspace = new Workspace<RequestEnvironment>(_repository.LoadAsync, _repository.SaveAsync);
        _service = new EnvironmentService(workspace, _settings, _secrets, ids);
    }

    [Test]
    public async Task CreateStoresTrimmedNameAndReturnsId()
    {
        var created = await _service.CreateAsync("  Staging  ", CancellationToken.None);

        var listed = await _service.ListAsync(CancellationToken.None);
        Assert.That(created.Value, Is.EqualTo(NewId));
        Assert.That(listed.Value?.Select(environment => environment.Name).ToList(), Is.EqualTo(new List<string> { "Staging" }));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Staging")]
    public async Task CreateRejectsEmptyOrDuplicateNames(string name)
    {
        await _service.CreateAsync("Staging", CancellationToken.None);

        var result = await _service.CreateAsync(name, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [Test]
    public async Task RenameFailsWhenEnvironmentDoesNotExist()
    {
        var result = await _service.RenameAsync(OtherId, "Other", CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    [Test]
    public async Task DeleteClearsActiveSelectionWhenActiveEnvironmentIsDeleted()
    {
        await _service.CreateAsync("Staging", CancellationToken.None);
        await _service.SetActiveAsync(NewId, CancellationToken.None);

        await _service.DeleteAsync(NewId, CancellationToken.None);
        var active = await _service.GetActiveAsync(CancellationToken.None);

        Assert.That(active.Value, Is.Null);
    }

    [Test]
    public async Task GetActiveReturnsSelectedEnvironment()
    {
        await _service.CreateAsync("Staging", CancellationToken.None);
        await _service.SetActiveAsync(NewId, CancellationToken.None);

        var active = await _service.GetActiveAsync(CancellationToken.None);

        Assert.That(active.Value?.Name, Is.EqualTo("Staging"));
    }

    [Test]
    public async Task GetActiveIsNullWhenNothingIsSelected()
    {
        var active = await _service.GetActiveAsync(CancellationToken.None);

        Assert.That(active.Value, Is.Null);
    }

    [Test]
    public async Task UpdateVariablesEncryptsPlainTextOfSecrets()
    {
        await _service.CreateAsync("Staging", CancellationToken.None);

        var result = await _service.UpdateVariablesAsync(
            NewId,
            [new VariableEdit("token", "abc", true, true), new VariableEdit("host", "example.com", false, true)],
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(VariablesOf(NewId), Is.EqualTo(new List<Variable>
        {
            new("token", "enc:abc", true),
            new("host", "example.com", false),
        }));
    }

    [Test]
    public async Task UpdateVariablesKeepsStoredCiphertextWithoutEncrypting()
    {
        await _service.CreateAsync("Staging", CancellationToken.None);
        await _service.UpdateVariablesAsync(NewId, [new VariableEdit("token", "abc", true, true)], CancellationToken.None);

        await _service.UpdateVariablesAsync(NewId, [new VariableEdit("token", "enc:abc", true, false)], CancellationToken.None);

        Assert.That(VariablesOf(NewId), Is.EqualTo(new List<Variable> { new("token", "enc:abc", true) }));
        A.CallTo(() => _secrets.Encrypt("enc:abc")).MustNotHaveHappened();
    }

    [Test]
    public async Task UpdateVariablesFailsAndSavesNothingWhenSecretsAreLocked()
    {
        await _service.CreateAsync("Staging", CancellationToken.None);
        var saves = _repository.SaveCount;
        A.CallTo(() => _secrets.Encrypt(A<string>._))
            .Returns(Result<string>.Failure(new Error(ErrorCategory.Security, "Locked.")));

        var result = await _service.UpdateVariablesAsync(NewId, [new VariableEdit("token", "abc", true, true)], CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Security));
        Assert.That(_repository.SaveCount, Is.EqualTo(saves));
    }

    [TestCase("")]
    [TestCase("bad{name}")]
    public async Task UpdateVariablesRejectsInvalidNames(string name)
    {
        await _service.CreateAsync("Staging", CancellationToken.None);

        var result = await _service.UpdateVariablesAsync(NewId, [new VariableEdit(name, "x", false, true)], CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [Test]
    public async Task UpdateVariablesRejectsDuplicateNames()
    {
        await _service.CreateAsync("Staging", CancellationToken.None);

        var result = await _service.UpdateVariablesAsync(
            NewId,
            [new VariableEdit("host", "a", false, true), new VariableEdit("host", "b", false, true)],
            CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    private List<Variable> VariablesOf(Guid environmentId)
    {
        return _repository.Stored.Single(environment => environment.Id == environmentId).Variables.ToList();
    }
}
