using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace ACAudioVCServer
{
    public class CritSect : IDisposable
    {
        private IntPtr pCriticalSection = IntPtr.Zero;

        private const int CritSectSize32 = 32;//24;
        private const int CritSectSize64 = 64;//40;

        public class Context : IDisposable
        {
            public Context(CritSect _c)
            {
                c = _c;

                if (c.pCriticalSection != IntPtr.Zero)
                    EnterCriticalSection(c.pCriticalSection);
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

                if (c.pCriticalSection != IntPtr.Zero)
                    LeaveCriticalSection(c.pCriticalSection);
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
            if (IntPtr.Size == 8)
                pCriticalSection = Marshal.AllocHGlobal(CritSectSize64);
            else
                pCriticalSection = Marshal.AllocHGlobal(CritSectSize32);

            InitializeCriticalSection(pCriticalSection);
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
            if (pCriticalSection != IntPtr.Zero)
            {
                DeleteCriticalSection(pCriticalSection);

                Marshal.FreeHGlobal(pCriticalSection);
                pCriticalSection = IntPtr.Zero;
            }
        }

        [DllImport("kernel32.dll")]
        private static extern void InitializeCriticalSection(IntPtr lpCriticalSection);

        [DllImport("kernel32.dll")]
        private static extern void DeleteCriticalSection(IntPtr lpCriticalSection);

        [DllImport("kernel32.dll")]
        private static extern void EnterCriticalSection(IntPtr lpCriticalSection);

        [DllImport("kernel32.dll")]
        private static extern void LeaveCriticalSection(IntPtr lpCriticalSection);
    }
}
