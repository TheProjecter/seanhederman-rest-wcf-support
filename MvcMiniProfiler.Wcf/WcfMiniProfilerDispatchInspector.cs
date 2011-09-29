 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel.Dispatcher;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace MvcMiniProfiler.Wcf
{
    public class WcfMiniProfilerDispatchInspector : IDispatchMessageInspector
    {
        public object AfterReceiveRequest(ref System.ServiceModel.Channels.Message request, System.ServiceModel.IClientChannel channel, System.ServiceModel.InstanceContext instanceContext)
        {
            if (request.Headers.MessageVersion != MessageVersion.None)
            {
                // Check to see if we have a request as part of this message
                var headerIndex = request.Headers.FindHeader(MiniProfilerRequestHeader.HeaderName, MiniProfilerRequestHeader.HeaderNamespace);
                if (headerIndex >= 0)
                {
                    var requestHeader = request.Headers.GetHeader<MiniProfilerRequestHeader>(headerIndex);
                    if (requestHeader != null)
                    {
                        MiniProfiler.Start();
                        return requestHeader;
                    }
                }
            }
            else if (WebOperationContext.Current != null || channel.Via.Scheme == "http" || channel.Via.Scheme == "https")
            {
                if (request.Properties.ContainsKey(HttpRequestMessageProperty.Name))
                {
                    var property = (HttpRequestMessageProperty)request.Properties[HttpRequestMessageProperty.Name];

                    var text = property.Headers[MiniProfilerRequestHeader.HeaderName];
                    if (!string.IsNullOrEmpty(text))
                    {
                        var header = MiniProfilerRequestHeader.FromHeaderText(text);
                        MiniProfiler.Start();
                        return header;
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("MVC Mini Profiler does not support EnvelopeNone unless HTTP is the transport mechanism");
            }

            return null;
        }

        public void BeforeSendReply(ref System.ServiceModel.Channels.Message reply, object correlationState)
        {
            var requestHeader = correlationState as MiniProfilerRequestHeader;
            MiniProfiler.Stop();
            var miniProfiler = MiniProfiler.Current;

            if (miniProfiler != null && requestHeader != null)
            {
                if (requestHeader.ExcludeTrivialMethods)
                {
                    miniProfiler.Root.RemoveTrivialTimings();
                }

                var header = new MiniProfilerResultsHeader
                {
                    ProfilerResults = miniProfiler
                };

                if (reply.Headers.MessageVersion != MessageVersion.None)
                {
                    var untypedHeader = new MessageHeader<MiniProfilerResultsHeader>(header)
                    .GetUntypedHeader(MiniProfilerResultsHeader.HeaderName, MiniProfilerResultsHeader.HeaderNamespace);

                    reply.Headers.Add(untypedHeader);
                }
                else if (reply.Properties.ContainsKey(HttpResponseMessageProperty.Name))
                {
                    HttpResponseMessageProperty property = (HttpResponseMessageProperty)reply.Properties[HttpResponseMessageProperty.Name];

                    property.Headers.Add(MiniProfilerResultsHeader.HeaderName, header.ToHeaderText());
                }
            }
            else
            {
                throw new InvalidOperationException("MVC Mini Profiler does not support EnvelopeNone unless HTTP is the transport mechanism");
            }

            //try
            //{
            //    var arrayOfIds = Settings.Storage.GetUnviewedIds(current.User).ToJson();
            //    // allow profiling of ajax requests
            //    response.AppendHeader("X-MiniProfiler-Ids", arrayOfIds);
            //}
            //catch { } // headers blew up


        }
    }
}
