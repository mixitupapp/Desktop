using System.Collections.Generic;

namespace MixItUp.API.V2.Models
{
    public class Counter
    {
        public string Name { get; set; }
        public double Amount { get; set; }
    }

    public class GetSingleCounterResponse
    {
        public Counter Counter { get; set; }
    }

    public class GetListOfCountersResponse
    {
        public int TotalCount { get; set; }
        public List<Counter> Counters { get; set; } = new List<Counter>();
    }

    public class CreateCounterRequest
    {
        public string Name { get; set; }
    }

    public class SetCounterRequest
    {
        public double? Amount { get; set; }
    }

    public class UpdateCounterRequest
    {
        public double? Amount { get; set; }
    }
}
