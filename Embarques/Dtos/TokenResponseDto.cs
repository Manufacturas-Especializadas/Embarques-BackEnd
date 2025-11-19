namespace Embarques.Dtos
{
    public class TokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;            
        public string TokenType {  get; set; } = string.Empty;
        public DateTime AccessTokenExpiration { get; set; }
    }
}