using RestDraft.Application.Abstractions;
using RestDraft.Application.Collections;
using RestDraft.UI.Services;

namespace RestDraft.UI.ViewModels;

public sealed record RequestTabServices(
    IRequestSender Sender,
    IRequestService Requests,
    IDestinationPicker Destinations,
    BodyFormatting Formatting,
    IFilePickerService Files,
    ISecretPrompt SecretPrompt);
