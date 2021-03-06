﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Raven.Storage.Managed.Impl
{
    public class PersistentDictionaryAdapter : IEnumerable<PersistentDictionary.ReadResult>
    {
        private readonly ThreadLocal<Guid> txId;
        private readonly PersistentDictionary persistentDictionary;
        private readonly Dictionary<string, SecondaryIndex> secondaryIndices = new Dictionary<string, SecondaryIndex>();

        public override string ToString()
        {
            return persistentDictionary.Name + " (" + Count +")";
        }

        public PersistentDictionaryAdapter(ThreadLocal<Guid> txId, PersistentDictionary persistentDictionary)
        {
            this.txId = txId;
            this.persistentDictionary = persistentDictionary;
        }

        public int Count
        {
            get { return persistentDictionary.ItemCount; }
        }

        public IEnumerable<JToken> Keys
        {
            get { return persistentDictionary.Keys; }
        }

        public bool Put(JToken key, byte[] value)
        {
            return persistentDictionary.Put(key, value, txId.Value);
        }

        public PersistentDictionary.ReadResult Read(JToken key)
        {
            return persistentDictionary.Read(key, txId.Value);
        }

        public bool Remove(JToken key)
        {
            return persistentDictionary.Remove(key, txId.Value);
        }

        public void Add(string name, Expression<Func<JToken, JToken>> func)
        {
            secondaryIndices[name] = persistentDictionary.AddSecondaryIndex(func); 
        }

        public IEnumerator<PersistentDictionary.ReadResult> GetEnumerator()
        {
            return persistentDictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public SecondaryIndex this[string indexName]
        {
            get { return secondaryIndices[indexName]; }
        }

        public bool UpdateKey(JToken key)
        {
            return persistentDictionary.UpdateKey(key, txId.Value);
        }
    }
}