using Embarques.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Embarques.Dtos;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Embarques.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<Users> _passwordHasher;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            AppDbContext context, 
            IConfiguration configuration, 
            ILogger<AuthService> logger
        )
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<Users>();
            _logger = logger;
        }

        public async Task<TokenResponseDto?> LoginAsync(LoginRequestDto request)
        {
            try
            {
                if (!request.PayRollNumber.HasValue)
                {
                    throw new ArgumentException("El número de nómina es requerido");
                }

                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    throw new ArgumentException("La contraseña es requerida");
                }

                var user = await _context.Users
                        .Include(u => u.Rol)
                        .FirstOrDefaultAsync(u => u.PayRollNumber == request.PayRollNumber);

                if(user == null)
                {
                    _logger.LogWarning($"Intento de login con el número de nómina no encontrado: {request.PayRollNumber}");
                    return null;
                }

                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

                if(result != PasswordVerificationResult.Success)
                {
                    _logger.LogWarning($"Password incorrecto para usuario: {request.PayRollNumber}");
                    return null;
                }

                _logger.LogInformation("Login exitoso");
                return await CreateTokenResponseAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el login", request.PayRollNumber);
                throw;
            }
        }

        public async Task<Users?> RegisterAsync(RegisterRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (!request.PayRollNumber.HasValue)
                {
                    throw new ArgumentException("El número de nómina es requerido");
                }

                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    throw new ArgumentException("La contraseña es requerida");
                }

                if (string.IsNullOrWhiteSpace(request.RoleName))
                {
                    throw new ArgumentException("El rol es requerido");
                }

                if(await _context.Users.AnyAsync(u => u.PayRollNumber == request.PayRollNumber))
                {
                    throw new InvalidOperationException($"Ya existe un usuario con el número de nómina: {request.PayRollNumber}");
                }

                var role = await _context.Roles
                                .FirstOrDefaultAsync(r => r.Name == request.RoleName);

                if(role == null)
                {
                    throw new ArgumentException($"El rol {request.RoleName} no existe");
                }

                var user = new Users
                {
                    Name = request.Name.Trim(),
                    PayRollNumber = request.PayRollNumber!.Value,
                    RolId = role.Id,
                    PasswordHash = _passwordHasher.HashPassword(null!, request.Password),
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Usuario registrado");

                return user;
            }
            catch(Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error durante el registro");
                throw;
            }
        }

        public async Task<bool> LogoutAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                
                await _context.SaveChangesAsync();

                _logger.LogInformation("Logout existoso");
                return true;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error durante el logout para el usuario");
                return false;
            }
        }              

        public async Task<TokenResponseDto?> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                    throw new ArgumentException("Refresh token es requerido");

                var user = await _context.Users
                    .Include(u => u.Rol)
                    .FirstOrDefaultAsync(u =>
                        u.RefreshToken == refreshToken &&
                        u.RefreshTokenExpiryTime > DateTime.UtcNow);

                if (user == null)
                {
                    _logger.LogWarning("Intento de refresh token con token inválido o expirado");
                    return null;
                }

                _logger.LogInformation("Refresh token exitoso para usuario: {PayRollNumber}", user.PayRollNumber);
                return await CreateTokenResponseAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el refresh token");
                throw;
            }
        }

        private async Task<TokenResponseDto> CreateTokenResponseAsync(Users user)
        {
            var accessToken = CreateAccessToken(user);
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
                _configuration.GetValue<int>("Jwt:RefreshTokenExpirationDays", 7)
            );

            await _context.SaveChangesAsync();

            var accessTokenExpiration = DateTime.UtcNow.AddMinutes(
                _configuration.GetValue<int>("Jwt:AccessTokenExpirationMinutes", 60)
            );

            return new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                AccessTokenExpiration = accessTokenExpiration,
                TokenType = "Bearer"
            };
        }

        private string CreateAccessToken(Users user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.Name),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Rol?.Name ?? "User"),
                new Claim("PayRollNumber", user.PayRollNumber.ToString()),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key no configurada")));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    _configuration.GetValue<int>("Jwt:AccessTokenExpirationMinutes", 60)
                ),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}