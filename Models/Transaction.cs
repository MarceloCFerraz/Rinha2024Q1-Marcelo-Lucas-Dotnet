public class Transaction
{
    public int? id { get; set; }
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

    public bool IsInvalid()
    {
        return (tipo != 'c' && tipo != 'd') // reject transactions that are not credit or debit
            || string.IsNullOrEmpty(descricao) // reject null or empty descriptions
            || descricao.Length > 10 // reject descriptions with more than 10 chars
            || valor % 1 != 0 // reject double values, but *.0 values are ok
            || valor <= 0; // reject negative values and transactions with value 0
    }

}