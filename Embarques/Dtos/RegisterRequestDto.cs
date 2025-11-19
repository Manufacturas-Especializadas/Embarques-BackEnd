namespace Embarques.Dtos
{
    public class RegisterRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public int? PayRollNumber { get; set; }
        public string Password { get; set; } = string.Empty;
        public string RoleName { get; set; } = "Usuario";
    }
}