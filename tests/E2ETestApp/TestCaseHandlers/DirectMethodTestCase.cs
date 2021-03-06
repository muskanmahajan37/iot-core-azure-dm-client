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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Diagnostics;

using Mock.Microsoft.Azure.Devices.Client;

namespace E2ETestApp
{
    class DirectMethodTestCase : TestCase
    {
        private const string JsonDirectMethodName = "directMethodName";

        public override void Dump()
        {
            Debug.WriteLine("Name              : " + _name);
            Debug.WriteLine("Desription        : " + _description);
            Debug.WriteLine("Method Name             : " + _methodName);
            Debug.WriteLine("Parameters              : " + _parameters.ToString());
            if (_expectedPresentReportedState != null)
            {
                Debug.WriteLine("Expected Present Output: " + _expectedPresentReportedState.ToString());
            }
            else
            {
                Debug.WriteLine("Expected Present Output: null");
            }
            if (_expectedAbsentReportedState != null)
            {
                Debug.WriteLine("Expected Absent Output: " + _expectedAbsentReportedState.ToString());
            }
            else
            {
                Debug.WriteLine("Expected Absent Output: null");
            }
            Debug.WriteLine("Expected return json    : " + (_expectedReturnJson == null ? "null" : _expectedReturnJson.ToString()));
            Debug.WriteLine("Expected return code    : " + _expectedReturnCode);
        }

        public static DirectMethodTestCase FromJson(ILogger logger, JObject testCaseJson)
        {
            logger.Log(LoggingLevel.Information, "        Parsing test case...");

            string name;
            if (!JsonHelpers.TryGetString(testCaseJson, Constants.TCJsonName, out name))
            {
                ReportError(logger, "Missing " + Constants.TCJsonName);
                return null;
            }

            string description;
            if (!JsonHelpers.TryGetString(testCaseJson, Constants.TCJsonDescription, out description))
            {
                ReportError(logger, "Missing " + Constants.TCJsonDescription);
                return null;
            }

            string methodName;
            if (!JsonHelpers.TryGetString(testCaseJson, JsonDirectMethodName, out methodName))
            {
                ReportError(logger, "Missing " + JsonDirectMethodName);
                return null;
            }

            JObject input;
            if (!JsonHelpers.TryGetObject(testCaseJson, Constants.TCJsonInput, out input))
            {
                ReportError(logger, "Missing " + Constants.TCJsonInput);
                return null;
            }

            JObject output;
            JObject expectedPresentReportedState = null;
            JObject expectedAbsentReportedState = null;
            JObject returnJson = null;
            JValue returnCode = null;
            if (!JsonHelpers.TryGetObject(testCaseJson, Constants.TCJsonOutput, out output))
            {
                ReportError(logger, "Missing " + Constants.TCJsonOutput);
                return null;
            }
            else
            {
                JObject deviceTwin = null;
                if (JsonHelpers.TryGetObject(output, Constants.TCJsonMethodDeviceTwin, out deviceTwin))
                {
                    if (JsonHelpers.TryGetObject(deviceTwin, Constants.TCJsonOutputPresent, out expectedPresentReportedState))
                    {
                        expectedPresentReportedState = (JObject)expectedPresentReportedState.DeepClone();
                    }

                    if (JsonHelpers.TryGetObject(deviceTwin, Constants.TCJsonOutputAbsent, out expectedAbsentReportedState))
                    {
                        expectedAbsentReportedState = (JObject)expectedAbsentReportedState.DeepClone();
                    }
                }
                JsonHelpers.TryGetObject(output, Constants.TCJsonMethodReturnJson, out returnJson);
                JsonHelpers.TryGetValue(output, Constants.TCJsonMethodReturnCode, out returnCode);
            }

            DirectMethodTestCase testCase = new DirectMethodTestCase();
            testCase._name = name;
            testCase._description = description;
            testCase._methodName = methodName;
            testCase._parameters = input;
            testCase._expectedPresentReportedState = expectedPresentReportedState;
            testCase._expectedAbsentReportedState = expectedAbsentReportedState;
            testCase._expectedReturnJson = returnJson;
            testCase._expectedReturnCode = (int)returnCode;
            return testCase;
        }

        public override async Task<bool> Execute(ILogger logger, TestParameters testParameters)
        {
            logger.Log(LoggingLevel.Information, "            Executing test case (" + _name + ")...");

            JObject resolvedParameters = (JObject)testParameters.ResolveParameters(_parameters);

            MethodResponse response = await _deviceController.InvokeDirectMethod(_methodName, resolvedParameters.ToString());
            string resultString = response.Result != null ? Encoding.UTF8.GetString(response.Result) : "";
            int resultCode = response.Status;

            logger.Log(LoggingLevel.Information, "              Analyzing results...");

            Debug.WriteLine("Final Result:");
            Debug.WriteLine("resultString: " + resultString);
            Debug.WriteLine("resultCode  : " + resultCode);

            List<string> errorList = new List<string>();
            bool result = true;

            if (_expectedReturnCode != response.Status)
            {
                string msg = "Unexpected return code: Expected [" + _expectedReturnCode + "], Actual [" + response.Status + "]";
                errorList.Add(msg);
                Debug.WriteLine(msg);
                result = false;
            }
            else
            {
                JObject actualReturnJson = (JObject)JsonConvert.DeserializeObject(resultString);
                if (_expectedReturnJson != null)
                {
                    result &= TestCaseHelpers.VerifyPropertiesPresent("returnValue", _expectedReturnJson, actualReturnJson, errorList);
                }

                Debug.WriteLine("---- Expected Present Result:");
                if (_expectedPresentReportedState != null)
                {
                    Debug.WriteLine(_expectedPresentReportedState.ToString());
                    result &= TestCaseHelpers.VerifyPropertiesPresent("deviceTwin", _expectedPresentReportedState, _actualReportedState, errorList);
                }

                Debug.WriteLine("---- Expected Absent Result:");
                if (_expectedAbsentReportedState != null)
                {
                    Debug.WriteLine(_expectedAbsentReportedState.ToString());
                    result &= TestCaseHelpers.VerifyPropertiesAbsent(_expectedAbsentReportedState, _actualReportedState, errorList);
                }
            }

            base.ReportResult(logger, result, errorList);

            return result;
        }

        private string _methodName;
        private JObject _parameters;
        private JObject _expectedPresentReportedState;
        private JObject _expectedAbsentReportedState;
        private JObject _expectedReturnJson;
        private int _expectedReturnCode;
    }
}