using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TradingJournal.Web.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public ApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _httpClient.BaseAddress = new Uri(_configuration["ApiBaseUrl"] ?? "http://localhost:3333/api/");
    }

    private void SetAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = _httpContextAccessor.HttpContext?.Session.GetString("Token");
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        SetAuthHeader();
        var response = await _httpClient.GetAsync(endpoint);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Clear session and throw to trigger redirect
            _httpContextAccessor.HttpContext?.Session.Clear();
            throw new UnauthorizedAccessException("Session expired. Please login again.");
        }
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<T?> PostAsync<T>(string endpoint, object data)
    {
        SetAuthHeader();
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<T?> PutAsync<T>(string endpoint, object data)
    {
        SetAuthHeader();
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task DeleteAsync(string endpoint)
    {
        SetAuthHeader();
        var response = await _httpClient.DeleteAsync(endpoint);
        response.EnsureSuccessStatusCode();
    }

    public async Task<T?> DeleteAsync<T>(string endpoint)
    {
        SetAuthHeader();
        var response = await _httpClient.DeleteAsync(endpoint);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _httpContextAccessor.HttpContext?.Session.Clear();
            throw new UnauthorizedAccessException("Session expired. Please login again.");
        }
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<T?> UploadFileAsync<T>(string endpoint, IFormFile file, string accountId, string? format = null)
    {
        SetAuthHeader();
        
        using var formContent = new MultipartFormDataContent();
        
        // Add the file
        using var fileStream = file.OpenReadStream();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "text/csv");
        formContent.Add(streamContent, "file", file.FileName);
        
        // Add accountId
        formContent.Add(new StringContent(accountId), "accountId");
        
        // Add format if specified
        if (!string.IsNullOrEmpty(format))
        {
            formContent.Add(new StringContent(format), "format");
        }

        var response = await _httpClient.PostAsync(endpoint, formContent);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            // Try to parse error response
            try
            {
                return JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                throw new HttpRequestException($"API Error: {response.StatusCode} - {responseContent}");
            }
        }
        
        return JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
