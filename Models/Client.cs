
public class Client
{
    public int id { get; set; }
    public int limite { get; set; }
    public int saldo { get; set; }

    public Client(int id, int limite, int saldo)
    {
        this.id = id;
        this.limite = limite;
        this.saldo = saldo;
    }

    public override string ToString()
    {
        return $"Client ID: {this.id}\nLimit: {this.limite}\nBalance: {this.saldo}";
    }

    public static bool IsInvalid(int client_id)
    {
        return client_id < 0 || client_id > 5;
    }
}