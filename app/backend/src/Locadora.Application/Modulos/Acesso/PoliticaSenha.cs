namespace Locadora.Application;

public static class PoliticaSenha
{
    public static bool EhValida(string? senha) =>
        !string.IsNullOrEmpty(senha) && senha.Length >= 8;
}
