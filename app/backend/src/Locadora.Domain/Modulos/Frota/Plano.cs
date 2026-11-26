namespace Locadora.Domain;

public enum ModalidadePlano
{
    Diario,
    Controlado,
    Livre,
}

public sealed class Plano : EntidadeNumerada
{
    public Guid GrupoId { get; set; }
    public Grupo? Grupo { get; set; }
    public ModalidadePlano Modalidade { get; set; }
    public decimal PrecoDiario { get; set; }
    public decimal? PrecoKm { get; set; }
    public int? FranquiaDiariaKm { get; set; }

    public static Plano Criar(Guid grupoId, ModalidadePlano modalidade, decimal? precoDiario,
        decimal? precoKm, int? franquiaDiariaKm, Guid empresaId, DateTimeOffset agora)
    {
        var parametros = Normalizar(modalidade, precoDiario, precoKm, franquiaDiariaKm);
        if (grupoId == Guid.Empty)
            throw new ValidacaoException("grupoId", "Selecione um grupo.");
        return new Plano
        {
            GrupoId = grupoId,
            Modalidade = modalidade,
            PrecoDiario = parametros.PrecoDiario,
            PrecoKm = parametros.PrecoKm,
            FranquiaDiariaKm = parametros.Franquia,
            EmpresaId = empresaId,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    public void AlterarParametros(decimal? precoDiario, decimal? precoKm, int? franquiaDiariaKm, DateTimeOffset agora)
    {
        var parametros = Normalizar(Modalidade, precoDiario, precoKm, franquiaDiariaKm);
        PrecoDiario = parametros.PrecoDiario;
        PrecoKm = parametros.PrecoKm;
        FranquiaDiariaKm = parametros.Franquia;
        AtualizadoEm = agora;
    }

    private static (decimal PrecoDiario, decimal? PrecoKm, int? Franquia) Normalizar(
        ModalidadePlano modalidade, decimal? precoDiario, decimal? precoKm, int? franquia)
    {
        var erros = new List<ErroValidacao>();
        if (!Enum.IsDefined(modalidade))
            erros.Add(new ErroValidacao("modalidade", "Selecione uma modalidade válida."));
        Validador.Adicionar(erros, "precoDiario", Validador.DecimalNaoNegativo(precoDiario));

        decimal? kmNormalizado = null;
        int? franquiaNormalizada = null;
        switch (modalidade)
        {
            case ModalidadePlano.Diario:
                Validador.Adicionar(erros, "precoKm", Validador.DecimalNaoNegativo(precoKm));
                kmNormalizado = precoKm;
                break;
            case ModalidadePlano.Controlado:
                Validador.Adicionar(erros, "precoKm", Validador.DecimalNaoNegativo(precoKm));
                Validador.Adicionar(erros, "franquiaDiariaKm", Validador.InteiroNaoNegativo(franquia));
                kmNormalizado = precoKm;
                franquiaNormalizada = franquia;
                break;
            case ModalidadePlano.Livre:
                break;
        }

        Validador.Lancar(erros);
        return (precoDiario!.Value, kmNormalizado, franquiaNormalizada);
    }
}
