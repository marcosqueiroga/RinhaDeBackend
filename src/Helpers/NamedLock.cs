namespace RinhaDeBackend.Helpers
{
    public sealed class NamedLock
    {
        private static readonly Dictionary<object, RefCounted<SemaphoreSlim>> semaphores = [];

        private sealed class RefCounted<T>(T value)
        {
            public int RefCount { get; set; } = 1;
            public T Value { get; private set; } = value;
        }

        private SemaphoreSlim GetOrCreate(object key)
        {
            RefCounted<SemaphoreSlim> item;

            lock (semaphores)
            {
                if (semaphores.TryGetValue(key, out item))
                {
                    ++item.RefCount;
                }
                else
                {
                    item = new RefCounted<SemaphoreSlim>(new SemaphoreSlim(1, 1));
                    semaphores[key] = item;
                }
            }

            return item.Value;
        }

        public IDisposable Lock(object key)
        {
            GetOrCreate(key).Wait();

            return new Releaser { Key = key };
        }

        public async Task<IDisposable> LockAsync(object key)
        {
            await GetOrCreate(key).WaitAsync().ConfigureAwait(false);

            return new Releaser { Key = key };
        }

        private sealed class Releaser : IDisposable
        {
            public object Key { get; set; }

            public void Dispose()
            {
                RefCounted<SemaphoreSlim> item;

                lock (semaphores)
                {
                    item = semaphores[Key];
                    --item.RefCount;

                    if (item.RefCount == 0)
                        semaphores.Remove(Key);
                }

                item.Value.Release();
            }
        }
    }
}