﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using Microsoft.Azure.Commands.PrivateDns.Utilities;

namespace Microsoft.Azure.Commands.PrivateDns.Models
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Commands.Common.Authentication;
    using Microsoft.Azure.Commands.Common.Authentication.Abstractions;
    using Microsoft.Azure.Commands.ResourceManager.Common.Tags;
    using Microsoft.Azure.Management.PrivateDns;
    using Microsoft.Azure.Management.PrivateDns.Models;
    using Microsoft.Rest.Azure;
    using ProjectResources = Microsoft.Azure.Commands.PrivateDns.Properties.Resources;
    using Sdk = Microsoft.Azure.Management.PrivateDns.Models;

    public class PrivateDnsClient
    {
        public const string DnsResourceLocation = "global";
        public const int TxtRecordMaxLength = 1024;
        public const int TxtRecordMinLength = 0;

        private readonly Dictionary<RecordType, Type> recordTypeValidationEntries = new Dictionary<RecordType, Type>()
        {
            {RecordType.A, typeof (ARecord)},
            {RecordType.AAAA, typeof (AaaaRecord)},
            {RecordType.CNAME, typeof (CnameRecord)},
            {RecordType.MX, typeof (MxRecord)},
            {RecordType.SOA, typeof (SoaRecord)},
            {RecordType.PTR, typeof (PtrRecord)},
            {RecordType.SRV, typeof (SrvRecord)},
            {RecordType.TXT, typeof (TxtRecord)}
        };

        public PrivateDnsClient(IAzureContext context)
            : this(AzureSession.Instance.ClientFactory.CreateArmClient<PrivateDnsManagementClient>(context, AzureEnvironment.Endpoint.ResourceManager))
        {
        }

        public PrivateDnsClient(IPrivateDnsManagementClient managementClient)
        {
            this.PrivateDnsManagementClient = managementClient;
        }

        public IPrivateDnsManagementClient PrivateDnsManagementClient { get; set; }

        public PSPrivateDnsZone CreatePrivateDnsZone(
            string name,
            string resourceGroupName,
            Hashtable tags)
        {
            var response = this.PrivateDnsManagementClient.PrivateZones.CreateOrUpdate(
                resourceGroupName,
                name,
                new PrivateZone
                {
                    Location = DnsResourceLocation,
                    Tags = TagsConversionHelper.CreateTagDictionary(tags, validate: true)

                },
                ifMatch: null,
                ifNoneMatch: "*");

            return ToPrivateDnsZone(response);
        }

        public PSPrivateDnsZone UpdatePrivateDnsZone(PSPrivateDnsZone zone, bool overwrite)
        {
            var response = this.PrivateDnsManagementClient.PrivateZones.CreateOrUpdate(
                zone.ResourceGroupName,
                zone.Name,
                new PrivateZone
                {
                    Location = DnsResourceLocation,
                    Tags = TagsConversionHelper.CreateTagDictionary(zone.Tags, validate: true),
                },
                ifMatch: overwrite ? null : zone.Etag,
                ifNoneMatch: null);

            return ToPrivateDnsZone(response);
        }

        public void DeletePrivateDnsZone(
            PSPrivateDnsZone zone,
            bool overwrite)
        {
            this.PrivateDnsManagementClient.PrivateZones.Delete(
                zone.ResourceGroupName,
                zone.Name,
                ifMatch: overwrite ? "*" : zone.Etag);
        }

        public PSPrivateDnsZone GetPrivateDnsZone(string name, string resourceGroupName)
        {
            return ToPrivateDnsZone(this.PrivateDnsManagementClient.PrivateZones.Get(resourceGroupName, name));
        }

        public List<PSPrivateDnsZone> ListPrivateDnsZonesInResourceGroup(string resourceGroupName)
        {
            List<PSPrivateDnsZone> results = new List<PSPrivateDnsZone>();
            IPage<PrivateZone> getResponse = null;
            do
            {
                getResponse = getResponse?.NextPageLink != null ? this.PrivateDnsManagementClient.PrivateZones.ListByResourceGroupNext(getResponse.NextPageLink) : this.PrivateDnsManagementClient.PrivateZones.ListByResourceGroup(resourceGroupName);

                results.AddRange(getResponse.Select(ToPrivateDnsZone));
            } while (getResponse?.NextPageLink != null);

            return results;
        }

        public List<PSPrivateDnsZone> ListPrivateDnsZonesInSubscription()
        {
            var results = new List<PSPrivateDnsZone>();
            IPage<PrivateZone> getResponse = null;
            do
            {
                getResponse = getResponse?.NextPageLink != null ? this.PrivateDnsManagementClient.PrivateZones.ListNext(getResponse.NextPageLink) : this.PrivateDnsManagementClient.PrivateZones.List();

                results.AddRange(getResponse.Select(ToPrivateDnsZone));
            } while (getResponse?.NextPageLink != null);

            return results;
        }


        public PSPrivateDnsZone GetDnsZoneHandleNonExistentZone(string zoneName, string resourceGroupName)
        {
            PSPrivateDnsZone retrievedZone = null;
            try
            {
                retrievedZone = this.GetPrivateDnsZone(zoneName, resourceGroupName);
            }
            catch (CloudException exception)
            {
                if (exception.Body.Code != "ResourceNotFound")
                {
                    throw;
                }
            }

            return retrievedZone;
        }

        private static PSPrivateDnsZone ToPrivateDnsZone(PrivateZone zone)
        {
            PrivateDnsUtils.GetResourceGroupNameFromResourceId(zone.Id, out var resourceGroupName);

            return new PSPrivateDnsZone()
            {
                Name = zone.Name,
                ResourceId = zone.Id,
                ResourceGroupName = resourceGroupName,
                Etag = zone.Etag,
                Tags = TagsConversionHelper.CreateTagHashtable(zone.Tags),
                NumberOfRecordSets = zone.NumberOfRecordSets,
                MaxNumberOfRecordSets = zone.MaxNumberOfRecordSets,
                NumberOfVirtualNetworkLinks = zone.NumberOfVirtualNetworkLinks,
                MaxNumberOfVirtualNetworkLinks = zone.MaxNumberOfVirtualNetworkLinks,
                NumberOfVirtualNetworkLinksWithRegistration = zone.NumberOfVirtualNetworkLinksWithRegistration,
                MaxNumberOfVirtualNetworkLinksWithRegistration = zone.MaxNumberOfVirtualNetworkLinksWithRegistration,
            };
        }

        public PSPrivateDnsVirtualNetworkLink CreatePrivateDnsLink(
            string name,
            string resourceGroupName,
            string zoneName,
            string virtualNetworkId,
            bool isRegistrationEnabled,
            Hashtable tags)
        {
            var response = this.PrivateDnsManagementClient.VirtualNetworkLinks.CreateOrUpdate(
                resourceGroupName,
                zoneName,
                name,
                new VirtualNetworkLink
                {
                    Location = DnsResourceLocation,
                    Tags = TagsConversionHelper.CreateTagDictionary(tags, validate: true),
                    VirtualNetwork = new SubResource()
                    {
                        Id = virtualNetworkId,
                    },
                    RegistrationEnabled = isRegistrationEnabled,

                },
                ifMatch: null,
                ifNoneMatch: "*");

            return ToPrivateDnsLink(response);
        }

        public PSPrivateDnsVirtualNetworkLink UpdatePrivateDnsLink(PSPrivateDnsVirtualNetworkLink link, bool overwrite)
        {
            var response = this.PrivateDnsManagementClient.VirtualNetworkLinks.CreateOrUpdate(
                link.ResourceGroupName,
                link.ZoneName,
                link.Name,
                new VirtualNetworkLink
                {
                    Location = DnsResourceLocation,
                    Tags = TagsConversionHelper.CreateTagDictionary(link.Tags, validate: true),
                    VirtualNetwork = new SubResource()
                    {
                        Id = link.VirtualNetworkId,
                    },
                    RegistrationEnabled = link.RegistrationEnabled,
                },
                ifMatch: overwrite ? null : link.Etag,
                ifNoneMatch: null);

            return ToPrivateDnsLink(response);
        }

        public void DeletePrivateDnsLink(
            PSPrivateDnsVirtualNetworkLink link,
            bool overwrite)
        {
            this.PrivateDnsManagementClient.VirtualNetworkLinks.Delete(
                link.ResourceGroupName,
                link.ZoneName,
                link.Name,
                ifMatch: overwrite ? "*" : link.Etag);
        }

        public PSPrivateDnsVirtualNetworkLink GetPrivateDnsLink(string name, string resourceGroupName, string zoneName)
        {
            return ToPrivateDnsLink(this.PrivateDnsManagementClient.VirtualNetworkLinks.Get(resourceGroupName, zoneName, name));
        }

        public List<PSPrivateDnsVirtualNetworkLink> ListPrivateDnsLinksInZone(string resourceGroupName, string zoneName)
        {
            var results = new List<PSPrivateDnsVirtualNetworkLink>();
            IPage<VirtualNetworkLink> getResponse = null;
            do
            {
                getResponse = getResponse?.NextPageLink != null ? this.PrivateDnsManagementClient.VirtualNetworkLinks.ListNext(getResponse.NextPageLink) : this.PrivateDnsManagementClient.VirtualNetworkLinks.List(resourceGroupName, zoneName);

                results.AddRange(getResponse.Select(ToPrivateDnsLink));
            } while (getResponse?.NextPageLink != null);

            return results;
        }

        public PSPrivateDnsVirtualNetworkLink GetLinkHandleNonExistentLink(string zoneName, string resourceGroupName, string linkName)
        {
            PSPrivateDnsVirtualNetworkLink retrievedLink = null;
            try
            {
                retrievedLink = this.GetPrivateDnsLink(linkName, resourceGroupName, zoneName);
            }
            catch (CloudException exception)
            {
                if (exception.Body.Code != "ResourceNotFound")
                {
                    throw;
                }
            }

            return retrievedLink;
        }

        private static PSPrivateDnsVirtualNetworkLink ToPrivateDnsLink(VirtualNetworkLink link)
        {
            PrivateDnsUtils.ParseVirtualNetworkId(link.Id, out var resourceGroupName, out var zoneName, out var linkName);

            return new PSPrivateDnsVirtualNetworkLink()
            {
                Name = link.Name,
                ResourceId = link.Id,
                ResourceGroupName = resourceGroupName,
                ZoneName = zoneName,
                Etag = link.Etag,
                Tags = TagsConversionHelper.CreateTagHashtable(link.Tags),
                VirtualNetworkId = link.VirtualNetwork.Id,
                RegistrationEnabled = link.RegistrationEnabled != null && (bool)link.RegistrationEnabled,
                ProvisioningState = link.ProvisioningState,
                VirtualNetworkLinkState = link.VirtualNetworkLinkState,
            };
        }
    }
}