using System.Threading;

namespace ACAVCServer
{
    internal abstract class WorkerThread
    {
        private enum ThreadStateValue
        {
            Stopped,
            Starting,
            Running,
            Stopping
        }

        private volatile ThreadStateValue ThreadState = ThreadStateValue.Stopped;
        private Thread Thread = null;

        public void Start()
        {
            if (ThreadState == ThreadStateValue.Running)
                return;

            ThreadState = ThreadStateValue.Starting;

            _Start_Pre();

            Thread = new Thread(_Thread_Internal);
            Thread.Start();

            while (ThreadState == ThreadStateValue.Starting)
                Thread.Sleep(10);

            _Start_Post();
        }

        public void Stop()
        {
            if (ThreadState == ThreadStateValue.Stopped)
                return;

            ThreadState = ThreadStateValue.Stopping;

            _Stop_Pre();

            while (ThreadState == ThreadStateValue.Stopping)
                Thread.Sleep(10);

            Thread = null;

            _Stop_Post();
        }

        protected bool WantStop()
        {
            // force a context release here so none of our threads are dumb and max CPU on accident by forgetting their own Sleep
            Thread.Sleep(1);

            return (ThreadState == ThreadStateValue.Stopping || ThreadState == ThreadStateValue.Stopped);
        }

        // called immediately before starting thread
        protected virtual void _Start_Pre()
        {

        }

        // called after thread has started
        protected virtual void _Start_Post()
        {

        }

        // called immediately before waiting for thread stop
        protected virtual void _Stop_Pre()
        {

        }

        // called after thread has stopped
        protected virtual void _Stop_Post()
        {

        }

        // called repeatedly while thread is supposed to be running.
        // (there is an internal context release, so no need to worry about maxing CPU usage)
        protected abstract void _Run();

        private void _Thread_Internal()
        {
            ThreadState = ThreadStateValue.Running;

            while (ThreadState == ThreadStateValue.Running)
            {
                _Run();
                Thread.Sleep(1);
            }

            ThreadState = ThreadStateValue.Stopped;
        }
    }
}
