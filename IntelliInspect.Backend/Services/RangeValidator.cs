public static class RangeValidator
{
    public static (bool IsValid, string Message) Validate(DateRangeRequest r, DatasetMetadata meta)
    {
        Console.WriteLine($"TrainEnd: {r.TrainEnd:O}, TestStart: {r.TestStart:O}");
        Console.WriteLine($"TestEnd: {r.TestEnd:O}, SimStart: {r.SimStart:O}");
        Console.WriteLine($"Meta Start: {meta.StartTimeStamp:O}, Meta End: {meta.EndTimeStamp:O}");

        if (r.TrainStart > r.TrainEnd)
            return (false, "Train start > end");
        if (r.TestStart > r.TestEnd)
            return (false, "Test start > end");
        if (r.SimStart > r.SimEnd)
            return (false, "Sim start > end");

        if (!(r.TrainEnd < r.TestStart))
            return (false, "Test must start after Train ends");
        if (!(r.TestEnd < r.SimStart))
            return (false, "Simulation must start after Test ends");

        if (r.TrainStart < meta.StartTimeStamp || r.SimEnd > meta.EndTimeStamp)
            return (false, "Date range outside dataset bounds");

        return (true, "");
    }
}
