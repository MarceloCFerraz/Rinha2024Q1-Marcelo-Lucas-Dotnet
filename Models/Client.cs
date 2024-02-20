public class Client
{
    public int id { get; set; }
    public int limite { get; set; }
    public int saldo { get; set; }

    public override string ToString()
    {
        return $"Client ID: {this.id}\nLimit: {this.limite}\nBalance: {this.saldo}";
    }
}