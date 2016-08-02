using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Table;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using AzureTableCopyLib;

namespace AzureTableCopyStep2Web
{

    public class Functions
    {
        public static void CopyTableEntries(
            [QueueTrigger("copyinput")] string copyRequestMsg,
            [Table("copyjobtracker")] CloudTable copyJobTracker,
            TextWriter log)
        {

            int itemNr = 0;


            var copyRequestMsgObj = JsonConvert.DeserializeObject<CopyRequestMsg>(copyRequestMsg);

            CloudStorageAccount sourceStorageAccount = CloudStorageAccount.Parse(copyRequestMsgObj.sourceStorageConnectionString);
            CloudTable sourceTable = sourceStorageAccount.CreateCloudTableClient().GetTableReference(copyRequestMsgObj.sourceTableName);
            CloudStorageAccount destinationStorageAccount = CloudStorageAccount.Parse(copyRequestMsgObj.destinationStorageConnectionString);
            CloudTable destinationTable = destinationStorageAccount.CreateCloudTableClient().GetTableReference(copyRequestMsgObj.destinationTableName);

            TableQuerySegment<DynamicTableEntity> querySegment = null;
            TableQuery<DynamicTableEntity> query = new TableQuery<DynamicTableEntity>().Take(Config.MAX_ROWS);
            var batch = new TableBatchOperation();
            string currentPk = "";
            for (int i = 0; i < (Config.BATCH_SIZE/Config.MAX_ROWS); i++)
            {
                querySegment = sourceTable.ExecuteQuerySegmented(query, i == 0 ? copyRequestMsgObj.token : querySegment.ContinuationToken);
                foreach (DynamicTableEntity te in querySegment)
                {
                    try
                    {
                        if (currentPk == "")
                        {
                            currentPk = te.PartitionKey;
                        }
                        // only entities within the same partition can be batched
                        if (currentPk != te.PartitionKey)
                        {
                            // execute the current batch
                            if (batch.Count > 0)
                            {
                                destinationTable.ExecuteBatch(batch);
                                batch = new TableBatchOperation();
                            }
                            currentPk = te.PartitionKey;
                        }
                        batch.InsertOrMerge(te);
                        if (batch.Count >= 99)
                        {
                            // execute the current batch
                            destinationTable.ExecuteBatch(batch);
                            batch = new TableBatchOperation();
                        }
                        itemNr++;
                    }
                    catch (Exception ex)
                    {
                        log.WriteLine("Error - {0}", ex.InnerException);
                    }
                }
                // check if we need iterate further
                if (querySegment.ContinuationToken == null)
                {
                    break;
                }
            }
            if (batch.Count > 0)
            {
                // execute the remaining batch
                destinationTable.ExecuteBatch(batch);
            }
            copyJobTracker.Execute(TableOperation.InsertOrMerge(new ItemsCopiedEntity(copyRequestMsgObj, itemNr )));
            log.WriteLine("Finished - {0} items added to tables", itemNr);
        }
    }
}
