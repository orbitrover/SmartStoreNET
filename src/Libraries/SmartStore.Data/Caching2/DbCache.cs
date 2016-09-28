﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SmartStore.Core.Caching;

namespace SmartStore.Data.Caching2
{
	public class DbCache : IDbCache
	{
		private const string KEYPREFIX = "efcache:";
		private readonly ICacheManager _cache;

		public DbCache(ICacheManager innerCache)
		{
			_cache = innerCache;
		}

		public bool TryGet(string key, out object value)
		{
			value = null;

			var entry = _cache.Get<DbCacheEntry>(HashKey(key));

			if (entry != null)
			{
				value = entry.Value;
				return true;
			}

			return false;
		}

		public void Put(string key, object value, IEnumerable<string> dependentEntitySets, TimeSpan? duration)
		{
			key = HashKey(key);

			lock (String.Intern(key))
			{
				var entitySets = dependentEntitySets.Distinct().ToArray();
				var entry =  new DbCacheEntry { Value = value, EntitySets = entitySets };

				_cache.Set(key, entry, duration);

				var lookup = GetEntitySetToKeyLookup();
				bool lookupIsDirty = false;		

				foreach (var entitySet in entitySets)
				{
					HashSet<string> keys;

					if (!lookup.TryGetValue(entitySet, out keys))
					{
						keys = new HashSet<string>();
						lookup[entitySet] = keys;
						lookupIsDirty = true;
					}

					if (!keys.Contains(key))
					{
						keys.Add(key);
						lookupIsDirty = true;
					}
				}

				if (lookupIsDirty)
				{
					PutEntitySetToKeyLookup(lookup);
				}
			}
		}

		public void InvalidateSets(IEnumerable<string> entitySets)
		{
			var sets = entitySets.Distinct().ToArray();
			//throw new NotImplementedException();
		}

		public void InvalidateItem(string key)
		{
			//key = HashKey(key);

			//lock (String.Intern(key))
			//{
			//	_cache.Remove(key);

			//	var lookup = GetEntitySetToKeyLookup();
			//	bool lookupIsDirty = false;

			//	foreach (var p in _entitySetToKey)
			//		p.Value.Remove(key);
			//}
		}

		private Dictionary<string, HashSet<string>> GetEntitySetToKeyLookup()
		{
			return _cache.Get(KEYPREFIX + "lookup", () => 
			{
				return new Dictionary<string, HashSet<string>>();
			});
		}

		private void PutEntitySetToKeyLookup(Dictionary<string, HashSet<string>> lookup)
		{
			_cache.Set(KEYPREFIX + "lookup", lookup);
		}

		private void RemoveEntitySetToKeyLookup()
		{
			_cache.Remove(KEYPREFIX + "lookup");
		}

		private static string HashKey(string key)
		{
			// Looking up large Keys can be expensive (comparing Large Strings), so if keys are large, hash them, otherwise if keys are short just use as-is
			if (key.Length <= 128)
				return KEYPREFIX + key;

			using (var sha = new SHA1CryptoServiceProvider())
			{
				key = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(key)));
				return KEYPREFIX + key;
			}
		}
	}

	public class DbCacheEntry
	{
		public object Value { get; set; }
		public string[] EntitySets { get; set; }
	}
}