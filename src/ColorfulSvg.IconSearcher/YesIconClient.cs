using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorfulSvg.IconSearcher;

internal sealed class YesIconClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly HttpClient _iconifyHttpClient;
    private readonly SemaphoreSlim _detailRequestLimiter = new(2, 2);

    public YesIconClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress ??= new Uri("https://yesicon.app/");
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ColorfulSvg.IconSearcher/1.0");

        _iconifyHttpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.iconify.design/")
        };
        _iconifyHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ColorfulSvg.IconSearcher/1.0");
    }

    public async Task<IconSearchResponse> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var uri = $"api/search?page=1&size={Math.Max(maxResults, 1)}&q={Uri.EscapeDataString(query.Trim())}&locale=en";
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<YesIconSearchResponse>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Unable to parse yesicon search response.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var allCandidates = new List<IconSearchCandidate>();
        var collectionOptions = new List<CollectionFilterOption>();

        foreach (var collection in payload.CollectionFilter ?? [])
        {
            var collectionCandidates = new List<IconSearchCandidate>();
            foreach (var icon in collection.Icons ?? [])
            {
                if (string.IsNullOrWhiteSpace(icon.Id) || !seen.Add(icon.Id))
                {
                    continue;
                }

                var candidate = new IconSearchCandidate(
                    icon.Id,
                    collection.Prefix ?? ExtractPrefix(icon.Id),
                    ExtractName(icon.Id),
                    collection.Name ?? collection.Prefix ?? ExtractPrefix(icon.Id));
                collectionCandidates.Add(candidate);
                allCandidates.Add(candidate);
            }

            if (collectionCandidates.Count > 0)
            {
                collectionOptions.Add(new CollectionFilterOption(
                    collection.Prefix ?? collectionCandidates[0].Prefix,
                    collection.Name ?? collectionCandidates[0].CollectionName,
                    collection.Count ?? collectionCandidates.Count));
            }
        }

        var visibleCandidates = allCandidates.Take(maxResults).ToArray();
        return new IconSearchResponse(visibleCandidates, allCandidates, collectionOptions);
    }

    public async Task<IconDetail> GetIconDetailAsync(IconSearchCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        await _detailRequestLimiter.WaitAsync(cancellationToken);
        try
        {
            var detail = await TryGetYesIconDetailAsync(candidate, cancellationToken);
            if (detail is not null)
            {
                return detail;
            }

            return await GetFallbackIconifyDetailAsync(candidate, cancellationToken);
        }
        finally
        {
            _detailRequestLimiter.Release();
        }
    }

    private async Task<IconDetail?> TryGetYesIconDetailAsync(IconSearchCandidate candidate, CancellationToken cancellationToken)
    {
        var delays = new[] { 0, 300, 900 };
        Exception? lastTransientException = null;

        for (var attempt = 0; attempt < delays.Length; attempt++)
        {
            if (delays[attempt] > 0)
            {
                await Task.Delay(delays[attempt], cancellationToken);
            }

            try
            {
                var uri = $"api/icon/{Uri.EscapeDataString(candidate.Id)}?locale=en";
                using var response = await _httpClient.GetAsync(uri, cancellationToken);

                if (IsTransientStatus(response.StatusCode))
                {
                    lastTransientException = new HttpRequestException($"yesicon detail endpoint returned {(int)response.StatusCode} ({response.ReasonPhrase}).");
                    continue;
                }

                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<YesIconDetailResponse>(stream, JsonOptions, cancellationToken)
                    ?? throw new InvalidOperationException($"Unable to parse icon details for '{candidate.Id}'.");

                var width = payload.IconData?.Width ?? payload.Height ?? 24;
                var height = payload.IconData?.Height ?? payload.Height ?? 24;
                var body = payload.IconData?.Body;
                if (string.IsNullOrWhiteSpace(body))
                {
                    throw new InvalidOperationException($"Icon '{candidate.Id}' does not contain SVG body data.");
                }

                var svg = $$"""
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {{width}} {{height}}" width="{{width}}" height="{{height}}" color="#111827">
                            {{body}}
                            </svg>
                            """;

                return new IconDetail(
                    candidate.Id,
                    candidate.Prefix,
                    candidate.Name,
                    payload.Name ?? candidate.CollectionName,
                    svg,
                    payload.Author?.Name,
                    payload.Author?.Url,
                    payload.License?.Title,
                    payload.License?.Spdx,
                    payload.License?.Url,
                    payload.Keywords ?? []);
            }
            catch (Exception ex) when (IsTransientException(ex))
            {
                lastTransientException = ex;
            }
            catch (Exception)
            {
                return null;
            }
        }

        if (lastTransientException is not null)
        {
            return null;
        }

        return null;
    }

    private async Task<IconDetail> GetFallbackIconifyDetailAsync(IconSearchCandidate candidate, CancellationToken cancellationToken)
    {
        var uri = $"{Uri.EscapeDataString(candidate.Prefix)}/{Uri.EscapeDataString(candidate.Name)}.svg";
        using var response = await _iconifyHttpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var svg = await response.Content.ReadAsStringAsync(cancellationToken);
        return new IconDetail(
            candidate.Id,
            candidate.Prefix,
            candidate.Name,
            candidate.CollectionName,
            svg,
            null,
            null,
            null,
            null,
            null,
            []);
    }

    private static bool IsTransientStatus(System.Net.HttpStatusCode statusCode)
    {
        var numericCode = (int)statusCode;
        return numericCode == 429 || numericCode >= 500;
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is HttpRequestException or TaskCanceledException;
    }

    private static string ExtractPrefix(string iconId)
    {
        var separatorIndex = iconId.IndexOf(':');
        return separatorIndex < 0 ? "icon" : iconId[..separatorIndex];
    }

    private static string ExtractName(string iconId)
    {
        var separatorIndex = iconId.IndexOf(':');
        return separatorIndex < 0 ? iconId : iconId[(separatorIndex + 1)..];
    }

    private sealed class YesIconSearchResponse
    {
        [JsonPropertyName("collectionFilter")]
        public List<YesIconCollection>? CollectionFilter { get; init; }
    }

    private sealed class YesIconCollection
    {
        [JsonPropertyName("prefix")]
        public string? Prefix { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("count")]
        public int? Count { get; init; }

        [JsonPropertyName("icons")]
        public List<YesIconSearchItem>? Icons { get; init; }
    }

    private sealed class YesIconSearchItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed class YesIconDetailResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("height")]
        public int? Height { get; init; }

        [JsonPropertyName("author")]
        public YesIconParty? Author { get; init; }

        [JsonPropertyName("license")]
        public YesIconLicense? License { get; init; }

        [JsonPropertyName("keywords")]
        public List<string>? Keywords { get; init; }

        [JsonPropertyName("iconData")]
        public YesIconData? IconData { get; init; }
    }

    private sealed class YesIconParty
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }
    }

    private sealed class YesIconLicense
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("spdx")]
        public string? Spdx { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }
    }

    private sealed class YesIconData
    {
        [JsonPropertyName("width")]
        public int? Width { get; init; }

        [JsonPropertyName("height")]
        public int? Height { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }
    }
}

internal sealed record IconSearchCandidate(string Id, string Prefix, string Name, string CollectionName);

internal sealed record CollectionFilterOption(string Prefix, string CollectionName, int MatchCount)
{
    public string Label => $"{CollectionName} / {Prefix} ({MatchCount})";
}

internal sealed record IconSearchResponse(
    IReadOnlyList<IconSearchCandidate> VisibleCandidates,
    IReadOnlyList<IconSearchCandidate> AllCandidates,
    IReadOnlyList<CollectionFilterOption> CollectionOptions);

internal sealed record IconDetail(
    string Id,
    string Prefix,
    string Name,
    string CollectionName,
    string SvgContent,
    string? AuthorName,
    string? AuthorUrl,
    string? LicenseTitle,
    string? LicenseSpdx,
    string? LicenseUrl,
    IReadOnlyList<string> Keywords);
