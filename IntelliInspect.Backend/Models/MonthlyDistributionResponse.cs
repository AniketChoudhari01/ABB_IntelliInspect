public class MonthlyDistributionResponse
{
    public string Status { get; set; } = "Valid";
    public int TrainCount { get; set; }
    public int TestCount { get; set; }
    public int SimulateCount { get; set; }
    public Dictionary<string, int> TrainMonthly { get; set; }
    public Dictionary<string, int> TestMonthly { get; set; }
    public Dictionary<string, int> SimMonthly { get; set; }
}
