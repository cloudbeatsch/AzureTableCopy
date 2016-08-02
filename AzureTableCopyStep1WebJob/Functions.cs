using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using AzureTableCopyLib;


namespace AzureTableCopyStep1Web
{
    public class Functions
    {
        public static void CreateContinuationTokens(
            [QueueTrigger("copyinputstep1")] string copyRequestMsg,
            [Queue("copyinputstep1")] ICollector<string> tableCopyStep1Queue,
            [Queue("copyinput")] ICollector<string> tableCopyQueue,
            [Table("copyjobtracker")] CloudTable copyJobTracker,
            TextWriter log)
        {
           

            var copyRequestMsgObj = JsonConvert.DeserializeObject<CopyRequestMsg>(copyRequestMsg);

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(copyRequestMsgObj.sourceStorageConnectionString);
            CloudTable sourceTable = storageAccount.CreateCloudTableClient().GetTableReference(copyRequestMsgObj.sourceTableName);

            // create the destination table in the first iteration
            if (copyRequestMsgObj.token == null)
            {
                CloudStorageAccount destinationStorageAccount = CloudStorageAccount.Parse(copyRequestMsgObj.destinationStorageConnectionString);
                CloudTable destinationTable = destinationStorageAccount.CreateCloudTableClient().GetTableReference(copyRequestMsgObj.destinationTableName);
                try
                {
                    destinationTable.CreateIfNotExists();
                }
                catch (Exception) {  }
                tableCopyQueue.Add(JsonConvert.SerializeObject(copyRequestMsgObj.GenerateNewMessageId()));
            }

            TableQuerySegment<TableEntity> querySegment = null;
            TableQuery<TableEntity> query = new TableQuery<TableEntity>().Take(Config.MAX_ROWS);
            for (int i = 0; i < Config.MAX_ITERATIONS; i++)
            {
                for (int j = 0; j < (Config.BATCH_SIZE/Config.MAX_ROWS) ; j++)
                {
                    querySegment = sourceTable.ExecuteQuerySegmented(query, (querySegment != null ? querySegment.ContinuationToken : copyRequestMsgObj.token));
                    copyRequestMsgObj.token = querySegment.ContinuationToken;
                    if (querySegment.ContinuationToken == null)
                    {
                        long nrOfItems = (j * Config.MAX_ROWS) + querySegment.Count();
                        copyJobTracker.Execute(TableOperation.InsertOrMerge(new ItemToCopyEntity(copyRequestMsgObj, nrOfItems )));
                        return;
                    }
                }
                copyJobTracker.Execute(TableOperation.InsertOrMerge(new ItemToCopyEntity(copyRequestMsgObj, Config.BATCH_SIZE)));
                tableCopyQueue.Add(JsonConvert.SerializeObject(copyRequestMsgObj.GenerateNewMessageId()));
                log.WriteLine("Contuniation token has been added");
            }
            tableCopyStep1Queue.Add(JsonConvert.SerializeObject(copyRequestMsgObj));
        }
    }
}
