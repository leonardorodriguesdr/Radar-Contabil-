namespace ControleContabil.Core;

/// <summary>Regra de negócio / dado inválido. A mensagem é exibida ao usuário.</summary>
public class ValidacaoException : Exception
{
    public ValidacaoException(string mensagem) : base(mensagem) { }
}

/// <summary>Outro usuário alterou o registro depois que ele foi carregado (controle otimista).</summary>
public class ConcorrenciaException : Exception
{
    public ConcorrenciaException() : base("O registro foi alterado por outro usuário.") { }
}
