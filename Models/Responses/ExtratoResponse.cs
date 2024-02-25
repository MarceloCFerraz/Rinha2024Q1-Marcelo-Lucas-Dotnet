public class ExtratoResponse
{
    public ExtratoDetails saldo { get; set; }
    public IEnumerable<TransactionHistory> ultimas_transacoes { get; set; }

    public ExtratoResponse(int total, DateTime data_extrato, int limite, List<TransactionHistory> ultimas_transacoes)
    {
        saldo = new ExtratoDetails(total, limite, data_extrato);
        this.ultimas_transacoes = ultimas_transacoes;
    }

    public ExtratoResponse()
    {
        saldo = new ExtratoDetails(0, limite: 0, DateTime.UtcNow);
        ultimas_transacoes = new List<TransactionHistory>();

    }
}