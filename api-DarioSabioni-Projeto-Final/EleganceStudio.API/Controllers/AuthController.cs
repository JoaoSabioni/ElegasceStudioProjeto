using EleganceStudio.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

namespace EleganceStudio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// POST /api/auth/login — Autenticação com BCrypt + User da BD
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest? request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = 401,
                Title = "Credenciais inválidas."
            });
        }

        var username = request.Username.Trim().ToLower();

        // 1. Buscar user por username (case-insensitive)
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username);

        // 2. Verificar password com BCrypt
        //    Mensagem genérica — não distinguir "user não existe" de "password errada"
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new ProblemDetails
            {
                Status = 401,
                Title = "Credenciais inválidas."
            });

        // 3. Construir claims
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, user.Role)
        };

        // Se é barbeiro, adicionar barberId como claim
        if (user.BarberId.HasValue)
            claims.Add(new Claim("barberId", user.BarberId.Value.ToString()));

        // 4. Gerar token JWT
        var secretKey = _config["Jwt:SecretKey"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Expiração: 8h para Barber, 4h para Admin
        var expiration = user.Role == "Admin"
            ? DateTime.UtcNow.AddHours(4)
            : DateTime.UtcNow.AddHours(8);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiration,
            signingCredentials: creds
        );

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            role = user.Role,
            barberId = user.BarberId,
            expiresAt = expiration
        });
    }
}

public record LoginRequest(string Username, string Password);
