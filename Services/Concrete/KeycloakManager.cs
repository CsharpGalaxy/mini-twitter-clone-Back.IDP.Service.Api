using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Business.DTOs;
using Business.DTOs.Authentication;
using keycloak_sample.Services.Abstract;
using keycloak_sample.Services.Concrete;
using keycloak_sample.Services.DTOs.Authentication;
using keycloak_sample.Services.DTOs.Common;
using keycloak_sample.Services.DTOs.Roles;
using keycloak_sample.Services.DTOs.Users;
using keycloak_sample.Services.ServiceOptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TS.Result;
using IResult = Microsoft.AspNetCore.Http.IResult;

public class KeycloakManager : IKeycloakService
{
    private readonly IOptions<KeycloakConfiguration> _options;
    private readonly HttpClient _httpClient;


    public KeycloakManager(IOptions<KeycloakConfiguration> options)
    {
        _options = options;
        _httpClient = new HttpClient();
    }
    public async Task<string> GetAdminTokenAsync(CancellationToken cancellationToken)
    {
        var tokenUrl = $"{_options.Value.HostName}/realms/master/protocol/openid-connect/token";

        var formData = new Dictionary<string, string>
    {
        { "client_id", "admin-cli" },
        { "grant_type", "password" },
        { "username", "admin" },
        { "password", "admin" }
    };

        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(formData));
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"❌ گرفتن توکن ناموفق: {json}");

        var token = JsonDocument.Parse(json).RootElement.GetProperty("access_token").GetString();
        return token!;
    }

    public async Task<string> GetAccessToken(CancellationToken cancellationToken)
    {
        string endpoint = $"{_options.Value.HostName}/realms/{_options.Value.Realm}/protocol/openid-connect/token";

        List<KeyValuePair<string, string>> data = new()
        {
            new("grant_type", "client_credentials"),
            new("client_id", _options.Value.ClientId),
            new("client_secret", _options.Value.ClientSecret)
        };


        Result<GetAccessTokenResponseDto> result =
            await PostUrlEncodedFormAsync<GetAccessTokenResponseDto>(endpoint, data, false, cancellationToken);

        return result.Data!.AccessToken;
    }

    public async Task<(bool IsSuccess, string Message)> RegisterAsync(RegisterDto request, CancellationToken cancellationToken)
    {
        var token = await GetAdminTokenAsync(cancellationToken);
        var realm = _options.Value.Realm;
        var baseUrl = $"{_options.Value.HostName}/admin/realms/{realm}";

        var userPayload = new
        {
            username = request.UserName,
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName,
            enabled = true,
            emailVerified = true
        };

        var createUserRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/users")
        {
            Content = new StringContent(JsonSerializer.Serialize(userPayload), Encoding.UTF8, "application/json")
        };
        createUserRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createUserResponse = await _httpClient.SendAsync(createUserRequest, cancellationToken);
        if (!createUserResponse.IsSuccessStatusCode)
        {
            var error = await createUserResponse.Content.ReadAsStringAsync(cancellationToken);
            return (false, $"❌ ساخت کاربر ناموفق: {error}");
        }

        // گرفتن userId از Location header
        var location = createUserResponse.Headers.Location?.ToString();
        var userId = location?.Split('/').LastOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "❌ شناسه کاربر قابل دریافت نیست");

        // تنظیم رمز عبور
        var passwordPayload = new
        {
            type = "password",
            value = request.Password,
            temporary = false
        };

        var setPasswordRequest = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/users/{userId}/reset-password")
        {
            Content = new StringContent(JsonSerializer.Serialize(passwordPayload), Encoding.UTF8, "application/json")
        };
        setPasswordRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var setPasswordResponse = await _httpClient.SendAsync(setPasswordRequest, cancellationToken);
        if (!setPasswordResponse.IsSuccessStatusCode)
        {
            var error = await setPasswordResponse.Content.ReadAsStringAsync(cancellationToken);
            return (false, $"❌ تنظیم رمز عبور ناموفق: {error}");
        }

        return (true, $"✅ کاربر با شناسه {userId} با موفقیت ثبت شد");
    }


    public async Task<(bool IsSuccess, string Message)> LoginAsync(LoginDto request, CancellationToken cancellationToken)
    {
        var url = $"{_options.Value.HostName}/realms/{_options.Value.Realm}/protocol/openid-connect/token";

        var data = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _options.Value.ClientId,
            ["client_secret"] = _options.Value.ClientSecret,
            ["username"] = request.UserName,
            ["password"] = request.Password
        };

        var result = await PostUrlEncodedFormAsync<GetAccessTokenResponseDto>(url, data.ToList(), false, cancellationToken);

        if (!result.IsSuccessful || string.IsNullOrWhiteSpace(result.Data?.AccessToken))
            return (false, result.ErrorMessages?.FirstOrDefault() ?? "ورود ناموفق");

        return (true, result.Data.AccessToken);
    }

    public async Task<Result<T>> PostAsync<T>(
     string endpoint,
     object data,
     bool requireToken = false,
     CancellationToken cancellationToken = default)
    {
        // افزودن توکن در صورت نیاز
        if (requireToken)
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // آماده‌سازی محتوا
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // ارسال درخواست
        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        // مدیریت خطاها
        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.BadRequest => HandleBadRequest<T>(responseBody),
                _ => HandleGenericError<T>(responseBody)
            };
        }

        // پاسخ بدون محتوا
        if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.NoContent)
            return Result<T>.Succeed(default!);

        // تبدیل پاسخ به مدل خروجی
        var result = JsonSerializer.Deserialize<T>(responseBody);
        return Result<T>.Succeed(result!);
    }

    private static Result<T> HandleBadRequest<T>(string responseBody)
    {
        var error = JsonSerializer.Deserialize<BadRequestErrorResponseDto>(responseBody);
        return Result<T>.Failure(error?.ErrorDescription ?? "error from request");
    }

    private static Result<T> HandleGenericError<T>(string responseBody)
    {
        var error = JsonSerializer.Deserialize<ErrorResponseDto>(responseBody);
        return Result<T>.Failure(error?.ErrorMessage ?? "error from server");
    }

    public async Task<Result<T>> PutAsync<T>(string endpoint, object data, bool reqToken = false, CancellationToken cancellationToken = default)
    {
        if (reqToken)
        {
            string token = await GetAdminTokenAsync(cancellationToken);

            _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }

        var content = new StringContent(
            JsonSerializer.Serialize(data),
            Encoding.UTF8,
            "application/json");

        var message = await _httpClient.PutAsync(endpoint, content, cancellationToken);

        var response = await message.Content.ReadAsStringAsync();

        if (!message.IsSuccessStatusCode)
        {
            if (message.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorResultForBadRequest = JsonSerializer.Deserialize<BadRequestErrorResponseDto>(response);

                return Result<T>.Failure(errorResultForBadRequest!.ErrorDescription);
            }

            var errorResultForOther = JsonSerializer.Deserialize<ErrorResponseDto>(response);

            return Result<T>.Failure(errorResultForOther!.ErrorMessage);
        }

        if (message.StatusCode == HttpStatusCode.Created || message.StatusCode == HttpStatusCode.NoContent)
        {
            return Result<T>.Succeed(default!);
        }

        var obj = JsonSerializer.Deserialize<T>(response);

        return Result<T>.Succeed(obj!);
    }

    public async Task<Result<T>> DeleteAsync<T>(string endpoint, bool reqToken = false,
        CancellationToken cancellationToken = default)
    {



        if (reqToken)
        {
            string token = await GetAdminTokenAsync(cancellationToken);

            _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }


        var message = await _httpClient.DeleteAsync(endpoint, cancellationToken);

        var response = await message.Content.ReadAsStringAsync();

        if (!message.IsSuccessStatusCode)
        {
            if (message.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorResultForBadRequest = JsonSerializer.Deserialize<BadRequestErrorResponseDto>(response);

                return Result<T>.Failure(errorResultForBadRequest!.ErrorDescription);
            }

            var errorResultForOther = JsonSerializer.Deserialize<ErrorResponseDto>(response);

            return Result<T>.Failure(errorResultForOther!.ErrorMessage);
        }

        if (message.StatusCode == HttpStatusCode.Created || message.StatusCode == HttpStatusCode.NoContent)
        {
            return Result<T>.Succeed(default!);
        }

        var obj = JsonSerializer.Deserialize<T>(response);

        return Result<T>.Succeed(obj!);
    }

    public async Task<Result<T>> DeleteAsync<T>(string endpoint, object data, bool reqToken = false, CancellationToken cancellationToken = default)
    {

        if (reqToken)
        {
            string token = await GetAdminTokenAsync(cancellationToken);

            _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }


        var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        string str = JsonSerializer.Serialize(data);
        request.Content = new StringContent(str, Encoding.UTF8, "application/json");


        var message = await _httpClient.SendAsync(request, cancellationToken);

        var response = await message.Content.ReadAsStringAsync();

        if (!message.IsSuccessStatusCode)
        {
            if (message.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorResultForBadRequest = JsonSerializer.Deserialize<BadRequestErrorResponseDto>(response);

                return Result<T>.Failure(errorResultForBadRequest!.ErrorDescription);
            }

            var errorResultForOther = JsonSerializer.Deserialize<ErrorResponseDto>(response);

            return Result<T>.Failure(errorResultForOther!.ErrorMessage);
        }

        if (message.StatusCode == HttpStatusCode.Created || message.StatusCode == HttpStatusCode.NoContent)
        {
            return Result<T>.Succeed(default!);
        }

        var obj = JsonSerializer.Deserialize<T>(response);

        return Result<T>.Succeed(obj!);
    }

    public async Task<Result<T>> PostUrlEncodedFormAsync<T>(
        string endpoint,
        List<KeyValuePair<string, string>> data,
        bool requireToken = false,
        CancellationToken cancellationToken = default)
    {
        if (requireToken)
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var content = new FormUrlEncodedContent(data);
        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return HandleErrorResponse<T>(response.StatusCode, responseBody);

        if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.NoContent)
            return Result<T>.Succeed(default!);

        var result = JsonSerializer.Deserialize<T>(responseBody);
        return Result<T>.Succeed(result!);
    }

    private static Result<T> HandleErrorResponse<T>(HttpStatusCode statusCode, string responseBody)
    {
        if (statusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            var error = JsonSerializer.Deserialize<BadRequestErrorResponseDto>(responseBody);
            return Result<T>.Failure(error?.ErrorDescription ?? "درخواست نامعتبر یا عدم احراز هویت");
        }

        var genericError = JsonSerializer.Deserialize<ErrorResponseDto>(responseBody);
        return Result<T>.Failure(genericError?.ErrorMessage ?? "خطای ناشناخته از سرور");
    }


    public async Task<Result<T>> GetAsync<T>(string endpoint, bool reqToken = false, CancellationToken cancellationToken = default)
    {
        if (reqToken)
        {
            string token = await GetAdminTokenAsync(cancellationToken);

            _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }

        var message = await _httpClient.GetAsync(endpoint, cancellationToken);

        var response = await message.Content.ReadAsStringAsync();

        if (!message.IsSuccessStatusCode)
        {
            if (message.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorResultForBadRequest = JsonSerializer.Deserialize<BadRequestErrorResponseDto>(response);

                return Result<T>.Failure(errorResultForBadRequest!.ErrorDescription);
            }

            var errorResultForOther = JsonSerializer.Deserialize<ErrorResponseDto>(response);

            return Result<T>.Failure(errorResultForOther!.ErrorMessage);
        }

        if (message.StatusCode == HttpStatusCode.Created || message.StatusCode == HttpStatusCode.NoContent)
        {
            return Result<T>.Succeed(default!);
        }

        var obj = JsonSerializer.Deserialize<T>(response);

        return Result<T>.Succeed(obj!);
    }


    public async Task<UserAuthInfoDto> GetCurrentUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(accessToken) as JwtSecurityToken;

            if (jsonToken == null)
            {
                throw new Exception("Invalid token format");
            }

            var userDto = new UserDto
            {
                Id = Guid.Parse(jsonToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? string.Empty),
                Username = jsonToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value,
                FirstName = jsonToken.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value,
                LastName = jsonToken.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value,
                Email = jsonToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
                EmailVerifed = bool.Parse(jsonToken.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value ?? "false"),
                Enabled = true
            };

            var resourceAccess = jsonToken.Claims
                .FirstOrDefault(c => c.Type == "resource_access")?
                .Value;

            var realm_access = jsonToken.Claims
         .FirstOrDefault(c => c.Type == "realm_access")?
         .Value;

            var roles = new List<RoleDto>();

            if (!string.IsNullOrEmpty(resourceAccess))
            {
                var resourceAccessObj = JsonSerializer.Deserialize<Dictionary<string, KeycloakResourceAccess>>(resourceAccess);

                if (resourceAccessObj != null && resourceAccessObj.TryGetValue("account", out var clientAccess))
                {
                    roles = clientAccess.Roles?
                        .Select(r => new RoleDto { Name = r })
                        .ToList() ?? new List<RoleDto>();
                }
                else
                {
                    roles = new List<RoleDto>(); // یا می‌تونی لاگ بزاری که کلید وجود نداشت
                }
            }
            if (!string.IsNullOrEmpty(realm_access))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var realmAccess = JsonSerializer.Deserialize<RealmAccessModel>(realm_access, options);

                if (realmAccess?.Roles != null)
                {
                    roles.AddRange(realmAccess.Roles.Select(r => new RoleDto { Name = r }));
                }

            }
            //var userRoles = await _userRolesService.GetAllUsersRolesByUserId(userDto.Id.ToString(), cancellationToken);
            //roles.AddRange(userRoles.Select(_ => new RoleDto { Id = Guid.Parse(_.Id), Name = _.Name }));
            return new UserAuthInfoDto
            {
                User = userDto,
                Roles = roles,
                AccessToken = accessToken
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"Error parsing user info from token: {ex.Message}", ex);
        }
    }
}

