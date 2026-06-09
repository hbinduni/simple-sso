using System.Text.Json.Serialization;

namespace Server.Models;

/// <summary>Standard API envelope: { success, data?, error?, message? }.</summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data) => new() { Success = true, Data = data };

    public static ApiResponse<object> Error(string message) => new() { Success = false, Error = message };
}

public record RefreshTokenResponse(string AccessToken, int ExpiresIn);

/// <summary>Body of GET /api/auth/me: the user plus OAuth group memberships (if any).</summary>
public record MeResponse(AuthUserProfile User, IReadOnlyList<string> Groups);

/// <summary>Public profile shape sent to the client, sourced entirely from the JWT claims.</summary>
public record AuthUserProfile(string Id, string Email, string Name, string Role);
