﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Defines a specific service that can be discovered.
    /// </summary>
    /// <seealso cref="ServiceDiscovery.Advertise(ServiceProfile)"/>
    public class ServiceProfile
    {
        private const string DefaultDomain = "local";
        private const short DefaultTTL = 60;
        /// <summary>
        ///   The domain name of the service.
        /// </summary>
        /// <value>
        ///   Defaults to "local".
        /// </value>
        public string Domain { get; }

        /// <summary>
        ///   A unique name for the service.
        /// </summary>
        /// <value>
        ///   Typically of the form "_<i>service</i>._tcp".
        /// </value>
        /// <remarks>
        ///   It consists of a pair of DNS labels, following the
        ///   <see href="https://www.ietf.org/rfc/rfc2782.txt">SRV records</see> convention.
        ///   The first label of the pair is an underscore character (_) followed by 
        ///   the <see href="https://tools.ietf.org/html/rfc6335">service name</see>. 
        ///   The second label is either "_tcp" (for application
        ///   protocols that run over TCP) or "_udp" (for all others). 
        /// </remarks>
        public string ServiceName { get; set; }

        /// <summary>
        ///   A unique identifier for the service instance.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public string InstanceName { get; set; }

        /// <summary>
        ///   The service name and domain.
        /// </summary>
        /// <value>
        ///   <see cref="ServiceName"/>.<see cref="Domain"/>
        /// </value>
        public string QualifiedServiceName => $"{ServiceName}.{Domain}";

        /// <summary>
        ///   The fully qualified name of the instance's host.
        /// </summary>
        /// <remarks>
        ///   This can be used to query the address records (A and AAAA)
        ///   of the service instance.
        /// </remarks>
        public string HostName { get; set; }

        /// <summary>
        ///   The instance name, service name and domain.
        /// </summary>
        /// <value>
        ///   <see cref="InstanceName"/>.<see cref="ServiceName"/>.<see cref="Domain"/>
        /// </value>
        public string FullyQualifiedName => $"{InstanceName}.{QualifiedServiceName}";

        /// <summary>
        ///   DNS resource records that are used to locate the service instance.
        /// </summary>
        /// <remarks>
        ///   All records should have the <see cref="ResourceRecord.Name"/> equal
        ///   to the <see cref="FullyQualifiedName"/>.
        ///   <para>
        ///   At a minimum the SRV and TXT records must be present.  Typically A/AAAA
        ///   records are also present.
        ///   </para>
        /// </remarks>
        public List<ResourceRecord> Resources { get; set; } = new List<ResourceRecord>();

        public PTRRecord servicePtrRecord { get; }
        public PTRRecord instancePtrRecord { get; }
        
        // Enforce multicast defaults, especially TTL.
        static ServiceProfile()
        {
            // Make sure MulticastService is inited.
            MulticastService.ReferenceEquals(null, null);
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class.
        /// </summary>
        /// <remarks>
        ///   All details must be filled in by the caller, especially the <see cref="Resources"/>.
        /// </remarks>
        private ServiceProfile()
        {
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class
        ///   with the specified details.
        /// </summary>
        /// <param name="instanceName">
        ///    A unique identifier for the specific service instance.
        /// </param>
        /// <param name="serviceName">
        ///   The <see cref="ServiceName">name</see> of the service.
        /// </param>
        /// <param name="port">
        ///   The TCP/UDP port of the service.
        /// </param>
        /// <param name="addresses">
        ///   The IP addresses of the specific service instance. If <b>null</b> then
        ///   <see cref="MulticastService.GetIPAddresses"/> is used.
        /// </param>
        /// <remarks>
        ///   The SRV, TXT and A/AAAA resoruce records are added to the <see cref="Resources"/>.
        /// </remarks>
        public ServiceProfile(string instanceName, string serviceName, ushort port, IEnumerable<IPAddress> addresses = null) :
            this(instanceName, serviceName, DefaultDomain, port, DefaultTTL, addresses)
        {
            
        }

        public ServiceProfile(string instanceName, string serviceName, string domainName, ushort port, short ttl, IEnumerable<IPAddress> addresses = null)
        {
            InstanceName = instanceName;
            ServiceName = serviceName;
            Domain = domainName;
            var fqn = FullyQualifiedName;

            var simpleServiceName = ServiceName
                .Replace("._tcp", "")
                .Replace("._udp", "")
                .TrimStart('_');
            HostName = $"{InstanceName}.{simpleServiceName}.{Domain}";
            Resources.Add(new SRVRecord
            {
                Name = fqn,
                Port = port,
                Target = HostName
            });
            Resources.Add(new TXTRecord
            {
                Name = fqn,
                Strings = { "txtvers=1" }
            });

            foreach (var address in addresses ?? MulticastService.GetLinkLocalAddresses())
            {
                Resources.Add(AddressRecord.Create(HostName, address));
            }
            
            servicePtrRecord = new PTRRecord { Name = ServiceName, DomainName = QualifiedServiceName, TTL = TimeSpan.FromSeconds(ttl)};
            instancePtrRecord = new PTRRecord { Name = QualifiedServiceName, DomainName = FullyQualifiedName, TTL = TimeSpan.FromSeconds(ttl)};
        }

        public void setTTl(short ttl)
        {
            servicePtrRecord.TTL = TimeSpan.FromSeconds(ttl);
            instancePtrRecord.TTL = TimeSpan.FromSeconds(ttl);
        }

        /// <summary>
        ///   Add a property of the service to the TXT record.
        /// </summary>
        /// <param name="key">
        ///   The name of the property.
        /// </param>
        /// <param name="value">
        ///   The value of the property.
        /// </param>
        public void AddProperty(string key, string value)
        {
            var txt = Resources.OfType<TXTRecord>().FirstOrDefault();
            if (txt == null)
            {
                txt = new TXTRecord { Name = FullyQualifiedName };
                Resources.Add(txt);
            }
            txt.Strings.Add(key + "=" + value);
        }
    }
}
