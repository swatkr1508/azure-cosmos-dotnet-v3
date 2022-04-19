﻿namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos;
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using System.Diagnostics;
    using System.IO;

    [TestClass]
    public class ContentSerializationPerformanceTests
    {
        private readonly QueryStatisticsAccumulator queryStatisticsAccumulator = new();
        private readonly string cosmosDatabaseId;
        private readonly string containerId;
        private readonly string contentSerialization;
        private readonly string query;
        private readonly int numberOfIterations;
        private readonly int warmupIterations;
        private readonly int MaxConcurrency;
        private readonly int MaxItemCount;
        private readonly bool appendRawDataToFile;
        private readonly bool useStronglyTypedIterator;
        public ContentSerializationPerformanceTests()
        {
            this.cosmosDatabaseId = System.Configuration.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.CosmosDatabaseId"];
            this.containerId = System.Configuration.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.ContainerId"];
            this.contentSerialization = System.Configuration.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.ContentSerialization"];
            this.query = System.Configuration.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.Query"];
            this.numberOfIterations = int.Parse(System.Configuration.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.NumberOfIterations"]);
            this.warmupIterations = int.Parse(System.Configuration.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.WarmupIterations"]);
            this.MaxConcurrency = int.Parse(System.Configuration.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.MaxConcurrency"]);
            this.MaxItemCount = int.Parse(System.Configuration.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.MaxItemCount"]);
            this.appendRawDataToFile = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.AppendRawDataToFile"]);
            this.useStronglyTypedIterator = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["ContentSerializationPerformanceTests.UseStronglyTypedIterator"]);
        }

        [TestMethod]
        public async Task ConnectEndpoint()
        {
            string endpoint = System.Configuration.ConfigurationManager.AppSettings["GatewayEndpoint"];
            string authKey = System.Configuration.ConfigurationManager.AppSettings["MasterKey"];
            using (CosmosClient client = new CosmosClient(endpoint, authKey,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct
                }))
            {
                await this.RunAsync(client);
            }
        }

        private async Task RunAsync(CosmosClient client)
        {
            Database cosmosDatabase = client.GetDatabase(this.cosmosDatabaseId);
            Container container = cosmosDatabase.GetContainer(this.containerId);
            string rawDataPath = "ContentSerializationPerformanceTestsRawData.csv";
            MetricsSerializer metricsSerializer = new MetricsSerializer();
            for (int i = 0; i < this.numberOfIterations; i++)
            {
                await this.RunQueryAsync(container);
            }
            using (TextWriter rawDataFile = new StreamWriter(path: rawDataPath, append: this.appendRawDataToFile))
            {
                metricsSerializer.SerializeAsync(rawDataFile, this.queryStatisticsAccumulator, this.numberOfIterations, this.warmupIterations, rawData: true);
            }
            using (TextWriter writer = Console.Out)
            {
                metricsSerializer.SerializeAsync(writer, this.queryStatisticsAccumulator, this.numberOfIterations, this.warmupIterations, rawData: false);
            }
        }

        private async Task RunQueryAsync(Container container)
        {
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxConcurrency = this.MaxConcurrency,
                MaxItemCount = this.MaxItemCount,
                CosmosSerializationFormatOptions = new CosmosSerializationFormatOptions(
                            contentSerializationFormat: this.contentSerialization,
                            createCustomNavigator: (content) => JsonNavigator.Create(content),
                            createCustomWriter: () => JsonWriter.Create(JsonSerializationFormat.Text))
            };

            if (this.useStronglyTypedIterator)
            {
                using (FeedIterator<States> iterator = container.GetItemQueryIterator<States>(
                    queryText: this.query,
                    requestOptions: requestOptions))
                {
                    await this.GetIteratorResponse(iterator);
                }
            }
            else
            {
                using (FeedIterator<string> distinctQueryIterator = container.GetItemQueryIterator<string>(
                        queryText: this.query,
                        requestOptions: requestOptions))
                {
                    await this.GetIteratorResponse(distinctQueryIterator);
                }
            }
        }

        private async Task GetIteratorResponse<T>(FeedIterator<T> feedIterator)
        {
            MetricsAccumulator metricsAccumulator = new MetricsAccumulator();
            Stopwatch totalTime = new Stopwatch();
            Stopwatch getTraceTime = new Stopwatch();
            while (feedIterator.HasMoreResults)
            {
                totalTime.Start();
                FeedResponse<T> response = await feedIterator.ReadNextAsync();
                {
                    getTraceTime.Start();
                    if (response.RequestCharge != 0)
                    {
                        metricsAccumulator.GetTrace(response, this.queryStatisticsAccumulator);
                    }
                    getTraceTime.Stop();
                }
                totalTime.Stop();
            }
            this.queryStatisticsAccumulator.queryStatisticsAccumulatorBuilder.queryMetrics.EndToEndTimeList = totalTime.ElapsedMilliseconds - getTraceTime.ElapsedMilliseconds;            
        }
    }
}
