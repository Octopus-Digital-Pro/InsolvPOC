using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/address")]
[Authorize]
public class AddressSearchController : ControllerBase
{
    private static readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "Insolvio/1.0 (address search)" } },
        Timeout = TimeSpan.FromSeconds(8),
    };

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Search Romanian addresses using OpenStreetMap Nominatim.
    /// Returns up to 10 results filtered to Romania.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 3)
            return Ok(Array.Empty<AddressResult>());

        var url = $"https://nominatim.openstreetmap.org/search"
                + $"?q={Uri.EscapeDataString(q)}"
                + "&countrycodes=ro"
                + "&format=json"
                + "&addressdetails=1"
                + "&limit=10"
                + "&accept-language=ro";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Ok(Array.Empty<AddressResult>());

            var json = await response.Content.ReadAsStringAsync(ct);
            var nominatimResults = JsonSerializer.Deserialize<List<NominatimResult>>(json, _jsonOpts)
                                   ?? new List<NominatimResult>();

            var results = nominatimResults
                .Select(r => new AddressResult(
                    DisplayName: r.DisplayName ?? "",
                    Road: r.Address?.Road ?? r.Address?.Pedestrian ?? r.Address?.Path ?? "",
                    HouseNumber: r.Address?.HouseNumber ?? "",
                    Suburb: r.Address?.Suburb ?? "",
                    City: r.Address?.City ?? r.Address?.Town ?? r.Address?.Village ?? r.Address?.Municipality ?? "",
                    County: r.Address?.County ?? r.Address?.State ?? "",
                    Postcode: r.Address?.Postcode ?? "",
                    Country: "Romania",
                    Lat: r.Lat,
                    Lon: r.Lon
                ))
                .ToList();

            return Ok(results);
        }
        catch (TaskCanceledException)
        {
            return Ok(Array.Empty<AddressResult>());
        }
        catch (HttpRequestException)
        {
            return Ok(Array.Empty<AddressResult>());
        }
    }

    // ── DTOs ──

    public record AddressResult(
        string DisplayName,
        string Road,
        string HouseNumber,
        string Suburb,
        string City,
        string County,
        string Postcode,
        string Country,
        string? Lat,
        string? Lon
    );

    private class NominatimResult
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; set; }
    }

    private class NominatimAddress
    {
        [JsonPropertyName("road")] public string? Road { get; set; }
        [JsonPropertyName("pedestrian")] public string? Pedestrian { get; set; }
        [JsonPropertyName("path")] public string? Path { get; set; }
        [JsonPropertyName("house_number")] public string? HouseNumber { get; set; }
        [JsonPropertyName("suburb")] public string? Suburb { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("town")] public string? Town { get; set; }
        [JsonPropertyName("village")] public string? Village { get; set; }
        [JsonPropertyName("municipality")] public string? Municipality { get; set; }
        [JsonPropertyName("county")] public string? County { get; set; }
        [JsonPropertyName("state")] public string? State { get; set; }
        [JsonPropertyName("postcode")] public string? Postcode { get; set; }
    }
}
