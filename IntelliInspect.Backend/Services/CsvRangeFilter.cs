using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

public class CsvRangeFilter
{
    public static (
        int TrainCount,
        int TestCount,
        int SimulateCount,
        Dictionary<string, int> TrainMonthly,
        Dictionary<string, int> TestMonthly,
        Dictionary<string, int> SimMonthly
    ) SplitCsvByRanges(string csvPath, DateRangeRequest ranges)
    {
        int train = 0,
            test = 0,
            sim = 0;

        var trainMonthly = new Dictionary<string, int>();
        var testMonthly = new Dictionary<string, int>();
        var simMonthly = new Dictionary<string, int>();

        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(
            reader,
            new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true }
        );

        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord!;

        while (csv.Read())
        {
            var timestampStr = csv.GetField("SyntheticTimestamp");

            if (DateTime.TryParse(timestampStr, out var timestamp))
            {
                var monthKey = timestamp.ToString("yyyy-MM");

                if (timestamp >= ranges.TrainStart && timestamp <= ranges.TrainEnd)
                {
                    train++;
                    if (!trainMonthly.ContainsKey(monthKey))
                        trainMonthly[monthKey] = 0;
                    trainMonthly[monthKey]++;
                }
                else if (timestamp >= ranges.TestStart && timestamp <= ranges.TestEnd)
                {
                    test++;
                    if (!testMonthly.ContainsKey(monthKey))
                        testMonthly[monthKey] = 0;
                    testMonthly[monthKey]++;
                }
                else if (timestamp >= ranges.SimStart && timestamp <= ranges.SimEnd)
                {
                    sim++;
                    if (!simMonthly.ContainsKey(monthKey))
                        simMonthly[monthKey] = 0;
                    simMonthly[monthKey]++;
                }
            }
        }

        return (train, test, sim, trainMonthly, testMonthly, simMonthly);
    }
}
