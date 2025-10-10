using System;
using System.Net;

namespace MixItUp.Distribution.Core
{
    public class DistributionException : Exception
    {
        public DistributionException(string message)
            : base(message)
        {
        }

        public DistributionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public HttpStatusCode? StatusCode { get; set; }

        public string Endpoint { get; set; }
    }
}
