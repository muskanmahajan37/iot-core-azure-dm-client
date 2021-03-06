﻿/*
Copyright 2017 Microsoft
Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
and associated documentation files (the "Software"), to deal in the Software without restriction, 
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
subject to the following conditions:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Microsoft.Devices.Management.DMDataContract;
using Microsoft.Devices.Management.Message;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation.Diagnostics;

namespace Microsoft.Devices.Management
{
    class UsoClientCmdHandler : IClientDirectMethodHandler
    {
        public string PropertySectionName
        {
            get
            {
                return UsoClientCmdDataContract.SectionName;
            }
        }

        public UsoClientCmdHandler(IClientHandlerCallBack callback, ISystemConfiguratorProxy systemConfiguratorProxy)
        {
            _systemConfiguratorProxy = systemConfiguratorProxy;
            _deviceManagementClient = callback;
        }

        public IReadOnlyDictionary<string, Func<string, Task<string>>> GetDirectMethodHandler()
        {
            return new Dictionary<string, Func<string, Task<string>>>()
                {
                    { UsoClientCmdDataContract.StartUsoClientCmdAsync, UsoClientCmdAsyncHandler }
                };
        }

        private async Task ReportCmdStatus(string status)
        {
            Logger.Log("ReportCmdStatus() invoked.", LoggingLevel.Verbose);

            UsoClientCmdDataContract.ReportedProperties reportedProperties = new UsoClientCmdDataContract.ReportedProperties();
            reportedProperties.response = status;
            await _deviceManagementClient.ReportPropertiesAsync(PropertySectionName, reportedProperties.ToJsonObject());

            StatusSection statusSection = new StatusSection(StatusSection.StateType.Completed);
            await _deviceManagementClient.ReportStatusAsync(PropertySectionName, statusSection);
        }

        private async Task InternalUsoClientCmdAsync(string jsonParamString)
        {
            Logger.Log("InternalUsoClientCmdAsync() invoked.", LoggingLevel.Verbose);

            await Helpers.EnsureErrorsLogged(_deviceManagementClient, PropertySectionName, async () =>
            {
                StatusSection status = new StatusSection(StatusSection.StateType.Pending);
                await _deviceManagementClient.ReportStatusAsync(PropertySectionName, status);

                // Parse json parameters
                var resetParams = JsonConvert.DeserializeObject<UsoClientCmdDataContract.CmdParams>(jsonParamString);

                // Construct request
                var request = new UsoClientCmdRequest();
                request.cmd = resetParams.cmd;

                // Send the request
                var response = await _systemConfiguratorProxy.SendCommandAsync(request);

                await ReportCmdStatus(UsoClientCmdDataContract.JsonScheduled);
            });
        }

        private Task<string> UsoClientCmdAsyncHandler(string jsonParam)
        {
            Logger.Log("UsoClientCmdAsync() invoked by direct method.", LoggingLevel.Verbose);

            InternalUsoClientCmdAsync(jsonParam).FireAndForget();  // Will do its own error reporting.

            StatusSection status = new StatusSection(StatusSection.StateType.Pending);
            return Task.FromResult<string>(status.AsJsonObject().ToString());
        }

        public async Task UsoClientCmdAsync(string cmd)
        {
            Logger.Log("UsoClientCmdAsync() invoked by application.", LoggingLevel.Verbose);

            UsoClientCmdDataContract.CmdParams jsonParam = new UsoClientCmdDataContract.CmdParams();
            jsonParam.cmd = cmd;

            string jsonParamString = JsonConvert.SerializeObject(jsonParam);
            await InternalUsoClientCmdAsync(jsonParamString);
        }

        private ISystemConfiguratorProxy _systemConfiguratorProxy;
        private IClientHandlerCallBack _deviceManagementClient;
    }
}
