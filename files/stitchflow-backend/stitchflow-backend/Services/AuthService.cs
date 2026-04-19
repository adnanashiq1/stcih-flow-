using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StitchFlow.API.Data;
using StitchFlow.API.DTOs;
using StitchFlow.API.Models;

namespace StitchFlow.API.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterTailorAsync(RegisterTailorRequest request);
    Task<AuthResponse> RegisterCustomerAsync(RegisterCustomerRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string refreshToken);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // ── REGISTER TAILOR ──────────────────────────────────────────
    public async Task<AuthResponse> RegisterTailorAsync(RegisterTailorRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email.ToLower()))
            throw new InvalidOperationException("Email already in use.");

        var user = new User
        {
            Name         = req.Name.Trim(),
            Email        = req.Email.ToLower().Trim(),
            Phone        = req.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role         = "tailor"
        };
        _db.Users.Add(user);

        var shopCode = await GenerateUniqueShopCodeAsync(req.ShopName);

        var profile = new TailorProfile
        {
            UserId         = user.Id,
            ShopName       = req.ShopName.Trim(),
            Specialization = req.Specialization,
            Address        = req.Address,
            City           = req.City,
            Bio            = req.Bio,
            ShopCode       = shopCode
        };
        _db.TailorProfiles.Add(profile);
        await _db.SaveChangesAsync();

        return await BuildAuthResponseAsync(user);
    }

    // ── REGISTER CUSTOMER ─────────────────────────────────────────
    public async Task<AuthResponse> RegisterCustomerAsync(RegisterCustomerRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email.ToLower()))
            throw new InvalidOperationException("Email already in use.");

        var user = new User
        {
            Name         = req.Name.Trim(),
            Email        = req.Email.ToLower().Trim(),
            Phone        = req.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role         = "customer"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(req.ShopCode))
        {
            var tailor = await _db.TailorProfiles
                .FirstOrDefaultAsync(t => t.ShopCode == req.ShopCode.ToUpper());

            if (tailor != null)
            {
                _db.CustomerTailorLinks.Add(new CustomerTailorLink
                {
                    CustomerId = user.Id,
                    TailorId   = tailor.Id
                });
                await _db.SaveChangesAsync();
            }
        }

        return await BuildAuthResponseAsync(user);
    }

    // ── LOGIN ────────────────────────────────────────────────────
    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var user = await _db.Users
            .Include(u => u.TailorProfile)
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower())
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated.");

        return await BuildAuthResponseAsync(user);
    }

    // ── REFRESH TOKEN ─────────────────────────────────────────────
    public async Task<AuthResponse> RefreshTokenAsync(string token)
    {
        var refreshToken = await _db.RefreshTokens
            .Include(r => r.User).ThenInclude(u => u.TailorProfile)
            .FirstOrDefaultAsync(r => r.Token == token)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (refreshToken.IsRevoked || refreshToken.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired or revoked.");

        refreshToken.IsRevoked = true;
        await _db.SaveChangesAsync();

        return await BuildAuthResponseAsync(refreshToken.User);
    }

    // ── REVOKE TOKEN ──────────────────────────────────────────────
    public async Task RevokeTokenAsync(string token)
    {
        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
        if (rt != null)
        {
            rt.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }

    // ── HELPERS ───────────────────────────────────────────────────
    private async Task<AuthResponse> BuildAuthResponseAsync(User user)
    {
        var jwtKey    = _config["Jwt:Key"] ?? throw new Exception("JWT key missing");
        var jwtIssuer = _config["Jwt:Issuer"] ?? "StitchFlow";
        var expiry    = DateTime.UtcNow.AddHours(2);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("name", user.Name),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwtIssuer, jwtIssuer, claims,
            expires: expiry, signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId    = user.Id,
            Token     = rawRefresh,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken  = accessToken,
            RefreshToken = rawRefresh,
            ExpiresAt    = expiry,
            User = new UserDto
            {
                Id    = user.Id,
                Name  = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Role  = user.Role,
                TailorProfile = user.TailorProfile == null ? null : new TailorProfileDto
                {
                    Id             = user.TailorProfile.Id,
                    ShopName       = user.TailorProfile.ShopName,
                    Specialization = user.TailorProfile.Specialization,
                    Address        = user.TailorProfile.Address,
                    City           = user.TailorProfile.City,
                    LogoUrl        = user.TailorProfile.LogoUrl,
                    ShopCode       = user.TailorProfile.ShopCode,
                    Bio            = user.TailorProfile.Bio,
                    IsVerified     = user.TailorProfile.IsVerified
                }
            }
        };
    }

    private async Task<string> GenerateUniqueShopCodeAsync(string shopName)
    {
        // SAFE PREFIX GENERATION:
        // Get the first word, remove non-alphanumeric characters
        var firstWord = shopName.Split(' ').First();
        
        // Ensure we don't exceed the length of the word itself
        int lengthToTake = Math.Min(5, firstWord.Length);
        var prefix = firstWord.ToUpper().Substring(0, lengthToTake);

        var rand = new Random();
        string code;
        do
        {
            // Generates a code like "HASSAN-123"
            code = $"{prefix}-{rand.Next(1000, 9999)}";
        }
        while (await _db.TailorProfiles.AnyAsync(t => t.ShopCode == code));

        return code;
    }
}