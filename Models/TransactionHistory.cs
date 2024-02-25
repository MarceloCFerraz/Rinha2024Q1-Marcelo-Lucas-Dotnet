public class TransactionHistory
{
    public int valor { get; set; }
    public char tipo { get; set; }
    public string descricao { get; set; }
    public DateTime realizada_em { get; set; }

    public TransactionHistory()
    {
        valor = 0;
        tipo = 'm';
        descricao = "m";
        realizada_em = DateTime.UtcNow;
    }
    public TransactionHistory(int valor, char tipo, string descricao, DateTime realizada_em)
    {
        this.valor = valor;
        this.tipo = tipo;
        this.descricao = descricao;
        this.realizada_em = realizada_em;
    }
}