﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using global::Azure.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class CosmosInstrumentation : ICosmosInstrumentation
    {
        private readonly DiagnosticScope scope;

        private HttpStatusCode statusCode;
        private double requestCharge;

        public CosmosInstrumentation(DiagnosticScope scope)
        {
            this.scope = scope;

            this.scope.Start();
        }

        public void Record(string attributeKey, object attributeValue)
        {
            if (this.scope.IsEnabled)
            {
                if (attributeKey.Equals(CosmosInstrumentationConstants.RequestCharge))
                {
                    this.requestCharge = Convert.ToDouble(attributeValue);
                }
                if (attributeKey.Equals(CosmosInstrumentationConstants.StatusCode))
                {
                    this.statusCode = (HttpStatusCode)attributeValue;
                }
                this.scope.AddAttribute(attributeKey, attributeValue);
            }
        }

        public void Record(ITrace trace)
        {
            if (this.scope.IsEnabled)
            {
                CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);

                this.Record(CosmosInstrumentationConstants.Region, diagnostics.GetContactedRegions());

                /*if (DiagnosticsFilterHelper.IsAllowed(
                        latency: diagnostics.GetClientElapsedTime(),
                        requestcharge: this.requestCharge,
                        statuscode: this.statusCode))
                {*/
                this.Record(CosmosInstrumentationConstants.RequestDiagnostics, diagnostics.ToString());
                //}
            }
        }

        public void MarkFailed(Exception exception)
        {
            if (this.scope.IsEnabled)
            {
                this.scope.AddAttribute(CosmosInstrumentationConstants.ExceptionMessage, exception.Message);
                this.scope.AddAttribute(CosmosInstrumentationConstants.ExceptionStacktrace, exception.StackTrace);
                this.scope.AddAttribute(CosmosInstrumentationConstants.ExceptionType, exception.GetType());

                this.scope.Failed(exception);
            }
        }

        public void Dispose()
        {
            this.scope.Dispose();
        }
    }
}
