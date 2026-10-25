using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed record RequestTabServices(
    IRequestSender Sender,
    IRequestService Requests,
    IDestinationPicker Destinations,
    BodyFormatting Formatting,
    IFilePickerService Files);
