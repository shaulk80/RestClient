using FakeItEasy;
using RestClient.Application.Abstractions;
using RestClient.Application.Bodies;
using RestClient.Application.Collections;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

internal static class RequestTabServicesFactory
{
    public static RequestTabServices Create(IRequestSender sender, IRequestService requests, IDestinationPicker destinations)
    {
        return new RequestTabServices(
            sender,
            requests,
            destinations,
            CreateFormatting(),
            A.Fake<IFilePickerService>());
    }

    public static BodyFormatting CreateFormatting()
    {
        return new BodyFormatting(new JsonBodyFormatter(), new XmlBodyFormatter());
    }
}
