using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Locadora.Api.Controllers;

public sealed record RegistroRequisicao(
    [Required, StringLength(256)][property: JsonPropertyName("username")] string Usuario,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, MinLength(8)][property: JsonPropertyName("password")] string Senha);
public sealed record LoginRequisicao([Required, StringLength(256)][property: JsonPropertyName("username")] string Usuario,
    [Required][property: JsonPropertyName("password")] string Senha);
public sealed record RefreshRequisicao([Required] string RefreshToken);
