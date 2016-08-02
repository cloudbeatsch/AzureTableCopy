using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureTableCopyLib;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System.Threading;
using System.IO;

namespace AzureTableCopy
{
    public class Table
    {
        public string source;
        public string destination;
    }
    public class TableCopyJob
    {
        public string sourceStorageConnectionString;
        public string destinationStorageConnectionString;
        public List<Table> tables;
    }

    class Program
    {
        static void Main(string[] args)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["QueueStorageAccount"].ToString());
            CloudTable trackerTable = storageAccount.CreateCloudTableClient().GetTableReference("copyjobtracker");
            trackerTable.CreateIfNotExists();

            if (args.Length == 0)
            {
                TableQuery<CopyJobEntity> query = new TableQuery<CopyJobEntity>();
                var resp = trackerTable.ExecuteQuery(query);
                foreach (var group in resp.GroupBy(x => x.PartitionKey))
                {
                    Console.WriteLine("job {0}  {1}/{2}", group.Key, group.Sum(x => x.ItemsCopied), group.Sum(x => x.ItemsToCopy));
                }
                Console.WriteLine("\n\n Press key to exit");
                Console.ReadKey();
            }
            else if (args.Length == 1)
            {
                TableCopyJob copyJob = JsonConvert.DeserializeObject<TableCopyJob>(File.OpenText(args[0]).ReadToEnd());

                CloudQueue queue = storageAccount.CreateCloudQueueClient().GetQueueReference("copyinputstep1");
                queue.CreateIfNotExists();

                CopyRequestMsg msg = new CopyRequestMsg()
                {
                    sourceStorageConnectionString = copyJob.sourceStorageConnectionString,
                    destinationStorageConnectionString = copyJob.destinationStorageConnectionString,
                };

                Dictionary<string, string> jobs = new Dictionary<string, string>();
                foreach (var table in copyJob.tables)
                {
                    jobs[table.source] = msg.CreateNewJob(table.source, table.destination);
                    queue.AddMessage(new CloudQueueMessage(JsonConvert.SerializeObject(msg)));
                }

                while (jobs.Count > 0)
                {
                    Console.Clear();
                    Console.WriteLine("Job Progress");
                    Console.WriteLine("============\n");
                    foreach (var job in jobs)
                    {
                        string filter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, job.Value);
                        TableQuery<CopyJobEntity> query = new TableQuery<CopyJobEntity>().Where(filter);
                        var resp = trackerTable.ExecuteQuery(query);
                        Console.WriteLine("job {0}  {1}/{2}", job.Key, resp.Sum(x => x.ItemsCopied), resp.Sum(x => x.ItemsToCopy));
                    }
                    Console.WriteLine("\n\n Press CTRL+C to exit \n\n");

                    for (int i = 0; i < 30; i++)
                    {
                        Thread.Sleep(1000);
                        Console.Write(".");
                    }
                }
            }
            else 
            {
                Console.WriteLine("AzureTableCopy usage:");
                Console.WriteLine("=====================\n");
                Console.WriteLine("List jobs");
                Console.WriteLine("     AzureTableCopy.exe");
                Console.WriteLine("Copy tables");
                Console.WriteLine("     AzureTableCopy.exe <path to copyjob.json>");
                return; 
            }
            
        }
    }
}
