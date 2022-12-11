using System;
using System.Threading;

namespace ACAVCServer
{
    internal class CritSect : IDisposable
    {
        private Mutex Mutex;

        public class Context : IDisposable
        {
            public Context(CritSect _c)
            {
                c = _c;

                c.Mutex.WaitOne();
            }

            CritSect c;

            ~Context()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);

                GC.SuppressFinalize(this);
            }

            volatile bool disposed = false;
            protected virtual void Dispose(bool disposing)
            {
                if (disposed)
                    return;

                disposed = true;

                c.Mutex.ReleaseMutex();
            }
        }

        public Context Lock
        {
            get
            {
                return new Context(this);
            }
        }

        public CritSect()
        {
            Mutex = new Mutex();
        }

        ~CritSect()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Mutex.Dispose();
            Mutex = null;
        }
    }
}
