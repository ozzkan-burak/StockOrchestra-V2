namespace Shared.Security;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.IdentityModel.Tokens;

/// <summary>
/// JWT Identity Service - Token üretimi ve doğrulaması için kullanılır.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - HS256: Simetrik anahtar (tek secret key)
/// - RS256: Asimetrik anahtar (public/private key çifti)
/// - Claims: Kullanıcı yetkileri ve bilgileri
/// - Internal Communication: Servisler arası token doğrulama
/// </remarks>
public class JwtIdentityService
{
    private readonly JwtConfig _config;
    private readonly SymmetricSecurityKey _key;
    
    public JwtIdentityService(JwtConfig config)
    {
        _config = config;
        
        var secretKey = config.SecretKey ?? throw new ArgumentNullException("SecretKey");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    }
    
    /// <summary>
    /// Kullanıcı için JWT token üretir.
    /// </summary>
    public string GenerateToken(
        Guid userId,
        string username,
        IEnumerable<string> roles,
        TimeSpan? expiresIn = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, username)
        };
        
        foreach (var role in roles)
        {
            claims.Add(new(ClaimTypes.Role, role));
        }
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(expiresIn ?? _config.TokenExpiration),
            SigningCredentials = new SigningCredentials(
                _key,
                SecurityAlgorithms.HmacSha256),
            Issuer = _config.Issuer,
            Audience = _config.Audience,
            NotBefore = DateTime.UtcNow
        };
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(token);
    }
    
    /// <summary>
    /// Token'ı doğrular ve claims'leri döndürür.
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidateIssuer = !string.IsNullOrEmpty(_config.Issuer),
                ValidIssuer = _config.Issuer,
                ValidateAudience = !string.IsNullOrEmpty(_config.Audience),
                ValidAudience = _config.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RequireExpirationTime = true
            };
            
            var principal = tokenHandler.ValidateToken(
                token,
                validationParameters,
                out _);
            
            return principal;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Token'ın süresinin dolup dolmadığını kontrol eder.
    /// </summary>
    public bool IsTokenExpired(string token)
    {
        var principal = ValidateToken(token);
        
        if (principal == null) return true;
        
        var expClaim = principal.FindFirst(JwtRegisteredClaimNames.Exp);
        
        if (expClaim == null) return true;
        
        var expTime = DateTimeOffset.FromUnixTimeSeconds(
            long.Parse(expClaim.Value));
        
        return expTime.UtcDateTime <= DateTime.UtcNow;
    }
    
    /// <summary>
    /// Token'dan kullanıcı ID'sini çıkarır.
    /// </summary>
    public Guid? GetUserId(string token)
    {
        var principal = ValidateToken(token);
        
        if (principal == null) return null;
        
        var subClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub);
        
        if (subClaim == null || !Guid.TryParse(subClaim.Value, out var userId))
            return null;
        
        return userId;
    }
    
    /// <summary>
    /// Token'dan kullanıcı adını çıkarır.
    /// </summary>
    public string? GetUsername(string token)
    {
        var principal = ValidateToken(token);
        
        if (principal == null) return null;
        
        return principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;
    }
    
    /// <summary>
    /// Token'dan kullanıcı rollerini çıkarır.
    /// </summary>
    public IEnumerable<string> GetRoles(string token)
    {
        var principal = ValidateToken(token);
        
        if (principal == null) yield break;
        
        foreach (var claim in principal.FindAll(ClaimTypes.Role))
        {
            yield return claim.Value;
        }
    }
    
    /// <summary>
    /// Refresh token üretir (uzun ömürlü).
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        
        return Convert.ToBase64String(randomNumber);
    }
    
    /// <summary>
    /// Token yenileme (refresh) işlemi.
    /// </summary>
    public string RefreshToken(string refreshToken)
    {
        return GenerateToken(
            Guid.NewGuid(),
            "refreshed_user",
            new[] { "User" },
            _config.RefreshTokenExpiration);
    }
}

/// <summary>
/// JWT yapılandırma ayarları
/// </summary>
public class JwtConfig
{
    public string SecretKey { get; set; } = string.Empty;
    
    public string Issuer { get; set; } = "StockOrchestra";
    
    public string Audience { get; set; } = "StockOrchestra-Api";
    
    public TimeSpan TokenExpiration { get; set; } = TimeSpan.FromMinutes(30);
    
    public TimeSpan RefreshTokenExpiration { get; set; } = TimeSpan.FromDays(7);
}

/// <summary>
/// Identity Result - Kimlik doğrulama sonucu
/// </summary>
public class IdentityResult
{
    public bool Succeeded { get; set; }
    
    public Guid? UserId { get; set; }
    
    public string? Username { get; set; }
    
    public string? Token { get; set; }
    
    public string? RefreshToken { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public IEnumerable<string> Roles { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Claims Keys - Standart claim anahtarları
/// </summary>
public static class ClaimKeys
{
    public const string UserId = "sub";
    public const string Username = "unique_name";
    public const string Email = "email";
    public const string Role = "role";
    public const string Permission = "permission";
}