using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlbumRobot.Sync.Core;

public sealed record QceDirectOptions(Uri BaseAddress, string AccessToken)
{
    public void Validate()
    {
        if (!BaseAddress.IsAbsoluteUri ||
            (BaseAddress.Scheme != Uri.UriSchemeHttp && BaseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("QCE base address must be an absolute HTTP(S) URI.", nameof(BaseAddress));
        }

        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            throw new ArgumentException("QCE access token is required.", nameof(AccessToken));
        }
    }
}

public sealed class QceApiException(HttpStatusCode statusCode, string endpoint)
    : Exception($"QCE request failed with {(int)statusCode} {statusCode} at {endpoint}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string Endpoint { get; } = endpoint;
}

/// <summary>
/// The smallest verified Direct API boundary for QCE 6.2.x.
///
/// Responses intentionally remain JsonDocument values until a real target-group
/// sample verifies the group, member, message and card schemas. The access token
/// is sent only in request headers and is never included in an exception message.
/// </summary>
public sealed class QceDirectClient
{
    private readonly Uri _baseAddress;
    private readonly string _accessToken;
    private readonly HttpClient _httpClient;

    public QceDirectClient(HttpClient httpClient, QceDirectOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _httpClient = httpClient;
        _baseAddress = EnsureTrailingSlash(options.BaseAddress);
        _accessToken = options.AccessToken;
    }

    public Task<JsonDocument> GetGroupsPageAsync(
        int page = 1,
        int limit = 200,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ValidatePositive(page, nameof(page));
        ValidatePositive(limit, nameof(limit));
        var path = $"/api/groups?page={page}&limit={limit}&forceRefresh={forceRefresh.ToString().ToLowerInvariant()}";
        return SendJsonAsync(HttpMethod.Get, path, body: null, cancellationToken);
    }

    public Task<JsonDocument> GetGroupMembersAsync(
        string groupCode,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupCode);
        var path = $"/api/groups/{Uri.EscapeDataString(groupCode)}/members?forceRefresh={forceRefresh.ToString().ToLowerInvariant()}";
        return SendJsonAsync(HttpMethod.Get, path, body: null, cancellationToken);
    }

    public Task<JsonDocument> FetchMessagesAsync(
        JsonElement peer,
        int page,
        int limit,
        QceMessageFilter filter,
        CancellationToken cancellationToken = default)
    {
        ValidatePositive(page, nameof(page));
        ValidatePositive(limit, nameof(limit));
        ArgumentNullException.ThrowIfNull(filter);

        var body = new QceFetchMessagesRequest(peer, page, limit, filter);
        return SendJsonAsync(HttpMethod.Post, "/api/messages/fetch", body, cancellationToken);
    }

    private async Task<JsonDocument> SendJsonAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(_baseAddress, path.TrimStart('/')));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.TryAddWithoutValidation("X-Access-Token", _accessToken);

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, QceJsonContext.Default),
                Encoding.UTF8,
                "application/json");
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new QceApiException(response.StatusCode, path);
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        try
        {
            return await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"QCE returned invalid JSON at {path}.", exception);
        }
    }

    private static Uri EnsureTrailingSlash(Uri address)
    {
        var absolute = address.AbsoluteUri.EndsWith('/') ? address.AbsoluteUri : address.AbsoluteUri + "/";
        return new Uri(absolute, UriKind.Absolute);
    }

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(parameterName, "Value must be greater than zero.");
    }
}

public sealed record QceMessageFilter(long StartTime, long EndTime);

public sealed record QceFetchMessagesRequest(
    [property: JsonPropertyName("peer")] JsonElement Peer,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("filter")] QceMessageFilter Filter);

internal static class QceJsonContext
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}
