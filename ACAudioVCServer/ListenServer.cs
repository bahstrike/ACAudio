// this worker thread's sole purpose is to asynchronously accept incoming clients and queue them for another thread to pick up via CollectClients

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace ACAudioVCServer
{
    public class ListenServer : WorkerThread
    {
        public readonly IPAddress IPAddress;
        public readonly int Port;

        public ListenServer(IPAddress _IPAddress, int _Port)
        {
            IPAddress = _IPAddress;
            Port = _Port;
        }

        protected sealed override void _Stop_Pre()
        {
            base._Stop_Pre();

            // this causes exception to break out of thread's blocking AcceptTcpClient() call
            if (listener != null)
                listener.Stop();
        }

        protected sealed override void _Stop_Post()
        {
            base._Stop_Post();
        
            // kill off any potential pending clients that weren't picked up by other logic
            TcpClient[] pendingClients = CollectClients();
                foreach (TcpClient client in pendingClients)
                    client.Close();
        }

        public TcpClient[] CollectClients()
        {
            TcpClient[] ret;
            using (clientsLock.Lock)
            {
                ret = clients.ToArray();
                clients.Clear();
            }
            return ret;
        }

        private TcpListener listener = null;
        private List<TcpClient> clients = new List<TcpClient>();
        private CritSect clientsLock = new CritSect();

        protected sealed override void _Run()
        {
            try
            {
                if (listener == null)
                {
                    listener = new TcpListener(IPAddress, Port);
                    listener.Start();

                    Server.Log($"Listening on {IPAddress}:{Port}");
                }

                    
                TcpClient client = listener.AcceptTcpClient();
                client.NoDelay = true;

                using (clientsLock.Lock)
                    clients.Add(client);
            }
            catch
            {
                if (listener != null)
                {
                    try
                    {
                        listener.Stop();
                    }
                    catch
                    {

                    }

                    listener = null;
                }
            }
        }
    }
}
