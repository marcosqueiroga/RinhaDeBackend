namespace RinhaDeBackend
{
    public record struct Transacao(int Valor, string Tipo, string Descricao, DateTime? Realizada_Em);
    public record class ResponseExtrato(ResponseSaldo Saldo, List<Transacao> Ultimas_Transacoes);
    public record class ResponseSaldo(int Total, int Limite, DateTime? Data_Extrato);
    public record class ResponseTransacao(int Limite, int Saldo);
}