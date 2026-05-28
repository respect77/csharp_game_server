using Microsoft.Extensions.Logging;
using Server.Api.Context;
using Server.Common;

namespace Server.Api.Handlers
{
    public partial class Handler
    {
        private DataContext _dataCtx = DataContext.Instance;
        private readonly ILogger<Handler> _logger;
        private ApiServerContext _servetCtx;
        public Handler(ApiServerContext servetCtx, ILogger<Handler> logger)
        {
            _servetCtx = servetCtx;
            _logger = logger;
        }
    }
}
