public class Transaction
{
    public int id { get; set; }
    public int valor { get; set; }
    public string descricao { get; set; } = "";
    public char tipo { get; set; } // Using 'string' here, if 'c' or 'd'
    public DateTime? realizada_em { get; set; }
    public int? id_cliente { get; set; }

    public override string ToString()
    {
        return $"Transaction ID: {this.id}\nValue: {this.valor}\nDescription: {this.descricao}\nType: {this.tipo}\nTimestamp: {this.realizada_em}\nClient ID: {this.id_cliente}";
    }

}
