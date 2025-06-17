namespace Online.Models
{
    // Models/ChartDataViewModel.cs
    public class ChartDataPoint
    {
        public string Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class ChartDataViewModel
    {
        public string ChartName { get; set; }
        public List<ChartDataPoint> Data { get; set; }
    }
}
