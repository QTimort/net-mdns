﻿using Common.Logging;
using Makaretu.Dns.Resolving;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Makaretu.Dns
{
    /// <summary>
    ///   DNS based Service Discovery is a way of using standard DNS programming interfaces, servers, 
    ///   and packet formats to browse the network for services.
    /// </summary>
    /// <seealso href="https://tools.ietf.org/html/rfc6763">RFC 6763 DNS-Based Service Discovery</seealso>
    public class ServiceDiscovery : IDisposable
    {
        /// <summary>
        ///   The service discovery service name.
        /// </summary>
        /// <value>
        ///   The service name used to enumerate other services.
        /// </value>
        public const string ServiceName = "_services._dns-sd._udp.local";
        
        private static readonly ILog Log = LogManager.GetLogger(typeof(ServiceDiscovery));
        
        private readonly bool ownsMdns;
        
        private List<ServiceProfile> profiles = new List<ServiceProfile>();

        /// <summary>
        ///   Gets the multicasting service.
        /// </summary>
        public MulticastService Mdns { get; private set; }

        /// <summary>
        ///   Gets the name server.
        /// </summary>
        /// <value>
        ///   Is used to answer questions.
        /// </value>
        public NameServer NameServer { get; } = new NameServer {
            Catalog = new Catalog(),
            AnswerAllQuestions = true
        };
        
        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class.
        /// </summary>
        public ServiceDiscovery()
            : this(new MulticastService())
        {
            ownsMdns = true;

            // Auto start.
            Mdns.Start();
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class with
        ///   the specified <see cref="MulticastService"/>.
        /// </summary>
        /// <param name="mdns">
        ///   The underlaying <see cref="MulticastService"/> to use.
        /// </param>
        public ServiceDiscovery(MulticastService mdns)
        {
            this.Mdns = mdns;
            mdns.QueryReceived += OnQuery;
            mdns.AnswerReceived += OnAnswer;
        }

        /// <summary>
        ///   Raised when a DNS-SD response is received.
        /// </summary>
        /// <value>
        ///   Contains the service name.
        /// </value>
        /// <remarks>
        ///   <b>ServiceDiscovery</b> passively monitors the network for any answers
        ///   to a DNS-SD query. When an anwser is received this event is raised.
        ///   <para>
        ///   Use <see cref="QueryAllServices"/> to initiate a DNS-SD question.
        ///   </para>
        /// </remarks>
        public event EventHandler<string> ServiceDiscovered;

        /// <summary>
        ///    Asks other MDNS services to send their service names.
        /// </summary>
        /// <remarks>
        ///   When an answer is received, <see cref="ServiceDiscovered"/> is raised.
        /// </remarks>
        public void QueryAllServices()
        {
            Mdns.SendQuery(ServiceName, type: DnsType.PTR);
        }

        /// <summary>
        ///   Advertise a service profile.
        /// </summary>
        /// <param name="service">
        ///   The service profile.
        /// </param>
        /// <remarks>
        ///   Any queries for the service or service instance will be answered with
        ///   information from the profile.
        /// </remarks>
        public void Advertise(ServiceProfile service)
        {
            profiles.Add(service);

            var catalog = NameServer.Catalog;
            catalog.Add(service.instancePtrRecord, authoritative: true);
            catalog.Add(service.servicePtrRecord, authoritative: true);

            foreach (var r in service.Resources)
            {
                catalog.Add(r, authoritative: true);
            }
        }

        void OnAnswer(object sender, MessageEventArgs e)
        {
            var msg = e.Message;

            // Any DNS-SD answers?
            var sd = msg.Answers
                .OfType<PTRRecord>()
                .Where(r => r.Name == ServiceName);
            foreach (var ptr in sd)
            {
                ServiceDiscovered?.Invoke(this, ptr.DomainName);
            }
        }

        void OnQuery(object sender, MessageEventArgs e)
        {
            var request = e.Message;

            if (Log.IsDebugEnabled)
                Log.Debug($"got query for {request.Questions[0].Name} {request.Questions[0].Type}");
            var response = NameServer.ResolveAsync(request).Result;
            if (response.Status == MessageStatus.NoError)
            {
                // Many bonjour browsers don't like DNS-SD response
                // with additional records.
                if (response.Answers.Any(a => a.Name == ServiceName))
                {
                    response.AdditionalRecords.Clear();
                }

                Mdns.SendAnswer(response);
                if (Log.IsDebugEnabled)
                    Log.Debug($"sent answer {response.Answers[0]}");
                //Console.WriteLine($"Response time {(DateTime.Now - request.CreationTime).TotalMilliseconds}ms");
            }
        }

#region IDisposable Support

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Mdns != null)
                {
                    Mdns.QueryReceived -= OnQuery;
                    Mdns.AnswerReceived -= OnAnswer;
                    if (ownsMdns)
                    {
                        Mdns.Dispose();
                    }
                    Mdns = null;
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
        }
#endregion
    }

}
