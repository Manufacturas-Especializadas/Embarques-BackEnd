namespace Embarques.Dtos
{
    public class LoginRequestDto
    {
        public int? PayRollNumber { get; set; }
        public string Password { get; set; } = string.Empty;
    }
}