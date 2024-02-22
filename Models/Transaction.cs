public class Transaction
{
    public int id { get; set; }
    public decimal valor { get; set; }
    public string descricao { get; set; } = "";
    public char tipo { get; set; } // Using 'string' here, if 'c' or 'd'
    public DateTime? realizada_em { get; set; }
    public int? id_cliente { get; set; }

    public override string ToString()
    {
        return $@"Transaction ID: {this.id}\n
            Value: {this.valor}\n
            Description: {this.descricao}\n
            Type: {this.tipo}\n
            Timestamp: {this.realizada_em}\n
            Client ID: {this.id_cliente}";
    }

}
