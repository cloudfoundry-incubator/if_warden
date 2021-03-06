﻿namespace IronFoundry.Warden.Handlers
{
    using System.Threading.Tasks;
    using Containers;
    using NLog;
    using Protocol;

    // MO: Added to ContainerClient
    public class InfoRequestHandler : ContainerRequestHandler
    {
        private readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly InfoRequest request;

        public InfoRequestHandler(IContainerManager containerManager, Request request)
            : base(containerManager, request)
        {
            this.request = (InfoRequest)request;
        }

        public override async Task<Response> HandleAsync()
        {
            log.Trace("Handle: '{0}'", request.Handle);
            return await BuildInfoResponseAsync();
        }
    }
}
