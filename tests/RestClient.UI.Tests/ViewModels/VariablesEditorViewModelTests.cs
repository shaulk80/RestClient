using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Environments;
using RestClient.Domain.Environments;
using RestClient.Domain.Results;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class VariablesEditorViewModelTests
{
    private static readonly Guid EnvironmentId = Guid.NewGuid();

    private IEnvironmentService _environments = null!;
    private ISecretPrompt _secrets = null!;

    [SetUp]
    public void SetUp()
    {
        _environments = A.Fake<IEnvironmentService>();
        A.CallTo(() => _environments.UpdateVariablesAsync(A<Guid>._, A<IReadOnlyList<VariableEdit>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        _secrets = A.Fake<ISecretPrompt>();
        A.CallTo(() => _secrets.EnsureUnlockedAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<bool>.Success(false)));
    }

    [Test]
    public async Task SaveWithOnlyStoredSecretsDoesNotAskForUnlock()
    {
        var viewModel = CreateViewModel([new Variable("token", "cipher", true), new Variable("host", "example.com", false)]);

        var saved = await viewModel.SaveAsync();

        Assert.That(saved, Is.True);
        A.CallTo(() => _secrets.EnsureUnlockedAsync(A<CancellationToken>._)).MustNotHaveHappened();
        List<VariableEdit> expected =
        [
            new("token", "cipher", true, false),
            new("host", "example.com", false, true),
        ];
        A.CallTo(() => _environments.UpdateVariablesAsync(
                EnvironmentId,
                A<IReadOnlyList<VariableEdit>>.That.Matches(edits => edits.SequenceEqual(expected)),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SaveWithNewSecretValueFailsWhenUserDeclinesUnlock()
    {
        var viewModel = CreateViewModel([]);
        viewModel.AddCommand.Execute(null);
        viewModel.Rows[0].Name = "token";
        viewModel.Rows[0].IsSecret = true;
        viewModel.Rows[0].Value = "abc";

        var saved = await viewModel.SaveAsync();

        Assert.That(saved, Is.False);
        Assert.That(viewModel.Message, Is.EqualTo("Unlock the secrets to save secret values."));
        A.CallTo(() => _environments.UpdateVariablesAsync(A<Guid>._, A<IReadOnlyList<VariableEdit>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Test]
    public async Task SaveRaisesSavedAfterStoringRows()
    {
        var viewModel = CreateViewModel([new Variable("host", "example.com", false)]);
        var raised = false;
        viewModel.Saved += (_, _) => raised = true;

        await viewModel.SaveAsync();

        Assert.That(raised, Is.True);
    }

    [Test]
    public void RemoveCommandRemovesTheRow()
    {
        var viewModel = CreateViewModel([new Variable("host", "example.com", false)]);

        viewModel.Rows[0].RemoveCommand.Execute(null);

        Assert.That(viewModel.Rows, Is.Empty);
    }

    private VariablesEditorViewModel CreateViewModel(IReadOnlyList<Variable> variables)
    {
        return new VariablesEditorViewModel(EnvironmentId, variables, _environments, _secrets);
    }
}
