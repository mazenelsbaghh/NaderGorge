'use strict';

const MAX_ENTRIES = 2_000;
const entries = new Map();

class MemoryIncrementalCacheHandler {
  async get(key) {
    return entries.get(key) ?? null;
  }

  async set(key, cacheValue) {
    if (cacheValue === null) {
      entries.delete(key);
      return;
    }

    entries.delete(key);
    entries.set(key, { value: cacheValue, lastModified: Date.now() });
    while (entries.size > MAX_ENTRIES) {
      entries.delete(entries.keys().next().value);
    }
  }

  async revalidateTag() {
    // A tag invalidation is rare and correctness matters more than retaining
    // unrelated per-process entries. Repopulate lazily from authoritative data.
    entries.clear();
  }

  resetRequestCache() {}
}

module.exports = MemoryIncrementalCacheHandler;
