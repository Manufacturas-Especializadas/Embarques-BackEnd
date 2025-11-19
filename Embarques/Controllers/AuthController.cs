using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Embarques.Dtos;
using Embarques.Models;
using Embarques.Services;
using System.Diagnostics;
using System.Security.Claims;

namespace Rechazos.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly AppDbContext _context;

        public AuthController(AuthService authService, AppDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        [HttpGet]
        [Route("GetRoles")]
        public async Task<IActionResult> GetRolesAsync()
        {
            var list = await _context.Roles.AsNoTracking().ToListAsync();

            if(list == null)
            {
                BadRequest("List empty");
            }

            return Ok(list);
        }

        [HttpPost]
        [Route("Logout")]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if(userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Usuario no autenticado" });
            }

            var success = await _authService.LogoutAsync(userId);

            if(!success)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            return Ok(new { message = "Sesión cerrada correctamente"});
        }

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var response = await _authService.LoginAsync(request);

            if (response == null)
            {
                return Unauthorized(new { message = "Nombre, número de nómina incorrectos" });
            }

            return Ok(response);
        }        

        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                var user = await _authService.RegisterAsync(request);

                if(user == null)
                {
                    return BadRequest(new { message = "Ya existe un usuario con ese nombre y número de nómina" });
                }

                return Ok(new { message = "Usuario registrado exitosamente" });
            }
            catch(Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        [Route("Refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return BadRequest("Refresh token es requerido");
            }

            var response = await _authService.RefreshTokenAsync(refreshToken);

            if(response == null)
            {
                return Unauthorized(new { message = "Refresh token inválido o expirado" });
            }

            return Ok(response);
        }
    }
}