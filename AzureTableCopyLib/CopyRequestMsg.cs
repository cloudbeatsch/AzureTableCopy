using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureTableCopyLib
{
    public class CopyJobBaseEntity : TableEntity
    {
        public CopyJobBaseEntity ()
        {
        }
        public CopyJobBaseEntity (CopyRequestMsg msg)
        {
            PartitionKey = msg.jobId;
            RowKey = msg.msgId;
            Request = JsonConvert.SerializeObject(msg); 
        }
        public string Request { get; set; }
    }
    public class CopyJobEntity : CopyJobBaseEntity
    {
        public long ItemsCopied { get; set; }
        public long ItemsToCopy { get; set; }

    }
    public class ItemsCopiedEntity : CopyJobBaseEntity
    {
        public ItemsCopiedEntity()
        {
        }
        public ItemsCopiedEntity(CopyRequestMsg msg, long _ItemsCopied)
            :base(msg)
        {
            ItemsCopied = _ItemsCopied;
        }
        public long ItemsCopied { get; set; }
    }
    public class ItemToCopyEntity : CopyJobBaseEntity
    {
        public ItemToCopyEntity()
        {
        }
        public ItemToCopyEntity(CopyRequestMsg msg, long _ItemsToCopy)
            : base(msg)
        {
            ItemsToCopy = _ItemsToCopy;
        }
        public long ItemsToCopy { get; set; }
    }
    public class Config
    {
        public const int MAX_ITERATIONS = 30;
        public const int MAX_ROWS = 1000;
        public const int BATCH_SIZE = 10000;
    }
    public class CopyRequestMsg
    {
        public string sourceStorageConnectionString;
        public string destinationStorageConnectionString;
        public string sourceTableName;
        public string destinationTableName;
        public TableContinuationToken token;
        public string jobId;
        public string msgId;

        public CopyRequestMsg GenerateNewMessageId()
        {
            msgId = Guid.NewGuid().ToString();
            return this;
        } 
        public string CreateNewJob(string _sourceTableName, string _destTableName)
        {
            sourceTableName = _sourceTableName;
            destinationTableName = _destTableName;
            token = null;
            jobId = Guid.NewGuid().ToString();
            return jobId;
        }
    }
}
