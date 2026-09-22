using System.Text.Json;
using System.Text.Json.Serialization;

namespace FotoHavn.App.UiVerification;

internal static class UiVerificationLaunch
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public static async Task<UiVerificationPresentationController?> TryCreateControllerAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var requestIndex = Array.IndexOf(args, "--ui-verification-request");
        if (requestIndex >= 0)
        {
            if (requestIndex + 1 >= args.Length)
            {
                throw new ArgumentException("--ui-verification-request requires a JSON file path.", nameof(args));
            }

            await using var stream = File.OpenRead(Path.GetFullPath(args[requestIndex + 1]));
            var request = await JsonSerializer.DeserializeAsync<UiVerificationRequest>(
                stream,
                JsonOptions,
                cancellationToken) ?? throw new InvalidDataException("The UI verification request is empty.");
            return await UiVerificationPresentationController.CreateAsync(request, cancellationToken);
        }

        var identityIndex = Array.IndexOf(args, "--ui-verification");
        if (identityIndex < 0)
        {
            return null;
        }

        if (identityIndex + 1 >= args.Length)
        {
            throw new ArgumentException("--ui-verification requires an approved injection identity.", nameof(args));
        }

        return await UiVerificationPresentationController.CreateAsync(
            new(args[identityIndex + 1]),
            cancellationToken);
    }
}
