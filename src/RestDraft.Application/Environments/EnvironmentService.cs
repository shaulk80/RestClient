using RestDraft.Application.Abstractions;
using RestDraft.Application.Secrets;
using RestDraft.Application.Workspaces;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Environments;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Environments;

public sealed class EnvironmentService : IEnvironmentService
{
    private const string ActiveKey = "environments.active";

    private readonly Workspace<RequestEnvironment> _workspace;
    private readonly ISettingsStore _settings;
    private readonly ISecretService _secrets;
    private readonly IIdGenerator _ids;

    public EnvironmentService(
        Workspace<RequestEnvironment> workspace,
        ISettingsStore settings,
        ISecretService secrets,
        IIdGenerator ids)
    {
        _workspace = workspace;
        _settings = settings;
        _secrets = secrets;
        _ids = ids;
    }

    public Task<Result<IReadOnlyList<RequestEnvironment>>> ListAsync(CancellationToken cancellationToken)
    {
        return _workspace.GetAllAsync(cancellationToken);
    }

    public async Task<Result<RequestEnvironment?>> GetActiveAsync(CancellationToken cancellationToken)
    {
        var stored = await _settings.GetAsync(ActiveKey, cancellationToken);
        if (!stored.IsSuccess)
        {
            return Result<RequestEnvironment?>.Failure(stored.Error!);
        }

        if (!Guid.TryParse(stored.Value, out var activeId))
        {
            return Result<RequestEnvironment?>.Success(null);
        }

        var listed = await _workspace.GetAllAsync(cancellationToken);
        if (!listed.IsSuccess)
        {
            return Result<RequestEnvironment?>.Failure(listed.Error!);
        }

        return Result<RequestEnvironment?>.Success(listed.Value!.FirstOrDefault(environment => environment.Id == activeId));
    }

    public Task<Result> SetActiveAsync(Guid? environmentId, CancellationToken cancellationToken)
    {
        var value = environmentId?.ToString() ?? string.Empty;
        return _settings.SetAsync(ActiveKey, value, cancellationToken);
    }

    public async Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var environment = new RequestEnvironment(_ids.NewId(), name.Trim(), []);
        var saved = await _workspace.UpdateAsync(
            environments => Add(environments, environment),
            cancellationToken);
        return saved.IsSuccess ? Result<Guid>.Success(environment.Id) : Result<Guid>.Failure(saved.Error!);
    }

    public Task<Result> RenameAsync(Guid environmentId, string name, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        return _workspace.UpdateAsync(environments => Rename(environments, environmentId, trimmed), cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        var deleted = await _workspace.UpdateAsync(environments => Delete(environments, environmentId), cancellationToken);
        if (!deleted.IsSuccess)
        {
            return deleted;
        }

        var active = await GetActiveAsync(cancellationToken);
        if (!active.IsSuccess)
        {
            return Result.Failure(active.Error!);
        }

        return active.Value?.Id == environmentId
            ? await SetActiveAsync(null, cancellationToken)
            : Result.Success();
    }

    public async Task<Result> UpdateVariablesAsync(
        Guid environmentId,
        IReadOnlyList<VariableEdit> edits,
        CancellationToken cancellationToken)
    {
        var stored = BuildStoredVariables(edits);
        if (!stored.IsSuccess)
        {
            return Result.Failure(stored.Error!);
        }

        return await _workspace.UpdateAsync(
            environments => ReplaceVariables(environments, environmentId, stored.Value!),
            cancellationToken);
    }

    // Encrypts the values the user typed for secrets. The result is what the repository stores.
    private Result<IReadOnlyList<Variable>> BuildStoredVariables(IReadOnlyList<VariableEdit> edits)
    {
        var stored = new List<Variable>();
        foreach (var edit in edits)
        {
            var name = VariableNameRules.ValidateName(edit.Name);
            if (!name.IsSuccess)
            {
                return Result<IReadOnlyList<Variable>>.Failure(name.Error!);
            }

            var value = StoredValue(edit);
            if (!value.IsSuccess)
            {
                return Result<IReadOnlyList<Variable>>.Failure(value.Error!);
            }

            stored.Add(new Variable(edit.Name.Trim(), value.Value!, edit.IsSecret));
        }

        var unique = VariableNameRules.ValidateUnique(stored);
        return unique.IsSuccess
            ? Result<IReadOnlyList<Variable>>.Success(stored)
            : Result<IReadOnlyList<Variable>>.Failure(unique.Error!);
    }

    private Result<string> StoredValue(VariableEdit edit)
    {
        return edit.IsSecret && edit.ValueIsPlainText
            ? _secrets.Encrypt(edit.Value)
            : Result<string>.Success(edit.Value);
    }

    private static Result<IReadOnlyList<RequestEnvironment>> Add(
        IReadOnlyList<RequestEnvironment> environments,
        RequestEnvironment environment)
    {
        var unique = ValidateName(environments, environment.Name, null);
        if (!unique.IsSuccess)
        {
            return Result<IReadOnlyList<RequestEnvironment>>.Failure(unique.Error!);
        }

        return Result<IReadOnlyList<RequestEnvironment>>.Success([.. environments, environment]);
    }

    private static Result<IReadOnlyList<RequestEnvironment>> Rename(
        IReadOnlyList<RequestEnvironment> environments,
        Guid environmentId,
        string name)
    {
        var found = Find(environments, environmentId);
        if (!found.IsSuccess)
        {
            return Result<IReadOnlyList<RequestEnvironment>>.Failure(found.Error!);
        }

        var unique = ValidateName(environments, name, environmentId);
        if (!unique.IsSuccess)
        {
            return Result<IReadOnlyList<RequestEnvironment>>.Failure(unique.Error!);
        }

        return Result<IReadOnlyList<RequestEnvironment>>.Success(
            Replace(environments, environmentId, environment => environment with { Name = name }));
    }

    private static Result<IReadOnlyList<RequestEnvironment>> Delete(IReadOnlyList<RequestEnvironment> environments, Guid environmentId)
    {
        var found = Find(environments, environmentId);
        if (!found.IsSuccess)
        {
            return Result<IReadOnlyList<RequestEnvironment>>.Failure(found.Error!);
        }

        return Result<IReadOnlyList<RequestEnvironment>>.Success(
            environments.Where(environment => environment.Id != environmentId).ToList());
    }

    private static Result<IReadOnlyList<RequestEnvironment>> ReplaceVariables(
        IReadOnlyList<RequestEnvironment> environments,
        Guid environmentId,
        IReadOnlyList<Variable> variables)
    {
        var found = Find(environments, environmentId);
        if (!found.IsSuccess)
        {
            return Result<IReadOnlyList<RequestEnvironment>>.Failure(found.Error!);
        }

        return Result<IReadOnlyList<RequestEnvironment>>.Success(
            Replace(environments, environmentId, environment => environment with { Variables = variables }));
    }

    private static Result ValidateName(IReadOnlyList<RequestEnvironment> environments, string name, Guid? renamingId)
    {
        var nameResult = ChildNameRules.ValidateName(name);
        if (!nameResult.IsSuccess)
        {
            return nameResult;
        }

        var taken = environments.Any(environment =>
            environment.Id != renamingId
            && string.Equals(environment.Name, name, StringComparison.OrdinalIgnoreCase));
        return taken
            ? Result.Failure(ResultErrors.Validation($"An environment named '{name}' already exists."))
            : Result.Success();
    }

    private static Result Find(IReadOnlyList<RequestEnvironment> environments, Guid environmentId)
    {
        return environments.Any(environment => environment.Id == environmentId)
            ? Result.Success()
            : Result.Failure(ResultErrors.NotFound("The environment"));
    }

    private static List<RequestEnvironment> Replace(
        IReadOnlyList<RequestEnvironment> environments,
        Guid environmentId,
        Func<RequestEnvironment, RequestEnvironment> change)
    {
        return environments.Select(environment => environment.Id == environmentId ? change(environment) : environment).ToList();
    }
}
