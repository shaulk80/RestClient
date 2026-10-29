using FakeItEasy;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Bodies;
using RestDraft.Application.Collections;
using RestDraft.Domain.Results;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

internal static class RequestTabServicesFactory
{
    // By default the secrets stay locked and the user declines to unlock them, so a send is not retried.
    public static RequestTabServices Create(
        IRequestSender sender,
        IRequestService requests,
        IDestinationPicker destinations,
        ISecretPrompt? secrets = null)
    {
        return new RequestTabServices(
            sender,
            requests,
            destinations,
            CreateFormatting(),
            A.Fake<IFilePickerService>(),
            secrets ?? CreateDecliningPrompt());
    }

    public static BodyFormatting CreateFormatting()
    {
        return new BodyFormatting(new JsonBodyFormatter(), new XmlBodyFormatter());
    }

    public static ISecretPrompt CreateDecliningPrompt()
    {
        var prompt = A.Fake<ISecretPrompt>();
        A.CallTo(() => prompt.EnsureUnlockedAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<bool>.Success(false)));
        return prompt;
    }
}
