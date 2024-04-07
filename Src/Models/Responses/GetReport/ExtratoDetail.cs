public class ExtratoDetails
{
    public int total { get; set; }
    public int limite { get; set; }
    public DateTime data_extrato { get; set; }


    public ExtratoDetails(int total, int limite, DateTime data_extrato)
    {
        this.total = total;
        this.limite = limite;
        this.data_extrato = data_extrato; // no need to convert to UTC because the database only stores and returns utc dates
    }
    public ExtratoDetails()
    {
        total = 0;
        limite = 0;
        data_extrato = DateTime.UtcNow;
    }
}