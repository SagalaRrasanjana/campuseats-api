using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CampusEats.Api.Data;
using CampusEats.Api.Dtos;
using CampusEats.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CampusEats.Api.Controllers;

[ApiController]
[Route("api/[controller]")]   // → /api/auth
public class AuthController : ControllerBase
{
    private readonly AppDbContext  _db;
    private readonly IConfiguration _cfg;

    public AuthController(AppDbContext db, IConfiguration cfg)
    {
        _db  = db;
        _cfg = cfg;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var taken = await _db.Users
            .AnyAsync(u => u.Email == dto.Email);

        if (taken) return Conflict("Email already used");  // 409

        var user = new User
        {
            Name         = dto.Name,
            Email        = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role         = "Customer"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            user.Id,
            user.Email,
            user.Role
        });
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        // Find user by email
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        // Verify password against stored hash
        if (user is null || !BCrypt.Net.BCrypt.Verify(
                dto.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");  // 401

        return Ok(new { token = CreateToken(user) });
    }

    // Builds the JWT
    private string CreateToken(User user)
    {
        var jwt   = _cfg.GetSection("Jwt");
        var key   = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(
                        key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email,           user.Email),
            new Claim(ClaimTypes.Role,            user.Role)  // used by RBAC
        };

        var token = new JwtSecurityToken(
            issuer:             jwt["Issuer"],
            audience:           jwt["Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(2),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}