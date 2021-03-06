﻿using System;
using System.IO;
using Raven.Database;

namespace Raven.Storage.Managed.Impl
{
    public class FileBasedPersistentSource : IPersistentSource
    {
        private readonly string basePath;
        private readonly string prefix;
        private readonly TransactionMode transactionMode;
        private readonly string dataPath;
        private readonly string logPath;

        private FileStream data;
        private FileStream log;

        public bool CreatedNew { get; set; }

        public FileBasedPersistentSource(string basePath, string prefix, TransactionMode transactionMode)
        {
            SyncLock = new object();
            this.basePath = basePath;
            this.prefix = prefix;
            this.transactionMode = transactionMode;
            dataPath = Path.Combine(basePath, prefix + ".data");
            logPath = Path.Combine(basePath, prefix + ".log");


            RecoverFromFailedRename(dataPath);
            RecoverFromFailedRename(logPath);

            CreatedNew = File.Exists(logPath) == false;

            OpenFiles();
        }

        private void OpenFiles()
        {
            log = new FileStream(logPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096,
                                 transactionMode == TransactionMode.Lazy ? FileOptions.SequentialScan : FileOptions.WriteThrough| FileOptions.SequentialScan
                );
            data = new FileStream(dataPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096,
                                  transactionMode == TransactionMode.Lazy ? FileOptions.RandomAccess : FileOptions.WriteThrough | FileOptions.RandomAccess
                );
        }

        #region IPersistentSource Members

        public object SyncLock
        {
            get;
            private set;
        }

        public Stream Data
        {
            get { return data; }
        }

        public Stream Log
        {
            get { return log; }

        }

        public void ReplaceAtomically(Stream newData, Stream newLog)
        {
            var newDataStream = ((FileStream)newData);
            string dataTempName = newDataStream.Name;
            newDataStream.Flush();
            newDataStream.Dispose();

            var newLogStream = ((FileStream)newLog);
            string logTempName = newLogStream.Name;
            newLogStream.Flush();
            newLogStream.Dispose();

            newData.Dispose();
            newLog.Dispose();

            log.Dispose();
            data.Dispose();

            string renamedDataFile = dataPath + ".rename_op";
            string renamedLogFile = logPath + ".rename_op";

            File.Move(dataPath, renamedDataFile);
            File.Move(logPath, renamedLogFile);

            File.Move(logTempName, logPath);
            File.Move(dataTempName, dataPath);

            File.Delete(renamedDataFile);
            File.Delete(renamedLogFile);

            OpenFiles();
        }

        public Stream CreateTemporaryStream()
        {
            string tempFile = Path.Combine(basePath, Path.GetFileName(Path.GetTempFileName()));
            return File.Open(tempFile, FileMode.Create, FileAccess.ReadWrite);
        }

        public void FlushData()
        {
            data.Flush(true);
        }

        public void FlushLog()
        {
            log.Flush(true);
        }

        public RemoteManagedStorageState CreateRemoteAppDomainState()
        {
            return new RemoteManagedStorageState
            {
                Path = basePath,
                Prefix = prefix
            };
        }

        #endregion

        private static void RecoverFromFailedRename(string file)
        {
            string renamedFile = file + ".rename_op";
            if (File.Exists(renamedFile) == false) // not in the middle of rename op, we are good
                return;

            if (File.Exists(file))
            // we successfully renamed the new file and crashed before we could remove the old copy
            {
                //just complete the op and we are good (committed)
                File.Delete(renamedFile);
            }
            else // we successfully renamed the old file and crashed before we could remove the new file
            {
                // just undo the op and we are good (rollback)
                File.Move(renamedFile, file);
            }
        }

        public void Dispose()
        {
            data.Dispose();
            log.Dispose();
        }

        public void Delete()
        {
            File.Delete(dataPath);
            File.Delete(logPath);
        }
    }
}