
/// <summary>
/// The main objective of this object is simply provide a type system for dapper's queries
/// </summary>
public class GetReportResponse
{
    public int customer_balance { get; set; }
    public int customer_limit { get; set; }
    public DateTime report_date { get; set; }
    public string last_transactions { get; set; } = ""; // This should be a IEnumerable<TransactionHistory>, but dapper does not convert it right away

    public override string ToString()
    {
        return $"""
            Balance: {customer_balance}\n
            Limit: {customer_limit}\n
            Report Date: {report_date.ToUniversalTime().ToString("s")}\n
            LastTransactions: {last_transactions}
        """;
    }
}