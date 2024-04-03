using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ApiSistemaCompra.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Ctrl_Conexion : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public Ctrl_Conexion(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // POST: api/Conexion/NombreBaseDatos
        [HttpPost("{nombreBaseDatos}")]
        public IActionResult Post(string nombreBaseDatos)
        {
          
            var connectionString = _configuration.GetConnectionString("conexion");

        
            if (!BaseDatosExiste(nombreBaseDatos, connectionString))
            {
                return BadRequest($"La base de datos '{nombreBaseDatos}' no existe.");
            }
           
            var parts = connectionString.Split(";");

           
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Trim().StartsWith("Initial Catalog="))
                {
                    parts[i] = $"Initial Catalog={nombreBaseDatos}";
                    break;
                }
            }

            connectionString = string.Join(";", parts);
             
     
            _configuration["ConnectionStrings:conexion"] = connectionString;

      
            string token = GenerarToken(nombreBaseDatos);
            return Ok(new { message = token });
        }

        private bool BaseDatosExiste(string nombreBaseDatos, string connectionString)
        {
            // Establecer una conexión temporal para verificar si la base de datos existe
            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    var command = new SqlCommand($"SELECT COUNT(*) FROM sys.databases WHERE name = '{nombreBaseDatos}'", connection);
                    var count = (int)command.ExecuteScalar();
                    return count > 0;
                }
                catch (SqlException)
                {
                    return false;
                }
            }
        }

        private string GenerarToken(string nombreBaseDatos)
        {
            // Configurar claims y generar token JWT
            Claim[] claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, _configuration["Jwt:Subject"]),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTime.UtcNow.ToString()),
                new Claim("@nombre", nombreBaseDatos)
            };

            SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            SigningCredentials signing = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            JwtSecurityToken token = new JwtSecurityToken(
                _configuration["Jwt:Issuer"],
                _configuration["Jwt:Audience"],
                claims,
                expires: DateTime.UtcNow.AddMinutes(30), 
                signingCredentials: signing);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
