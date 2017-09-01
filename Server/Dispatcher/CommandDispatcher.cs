using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class CommandDispatcher : MessageDispatcher, IDisposable
    {
        public static MessageDispatcher Instance = new CommandDispatcher();
        private CommandDispatcher()
        {
            stopFlag = false;
        }

        public void Start()
        {
            while (!stopFlag)
            {
                try
                {
                    Action action;
                    if (commandQueue.TryTake(out action))
                    {
                        action?.Invoke();
                    }
                }
                catch(Exception e)
                {

                }
            }
        }

        public void Stop()
        {
            // TODO: 安全退出
            stopFlag = true;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task OnDisconnected(Session session)
        {
            throw new NotImplementedException();
        }

        public Task OnMessageReceived(Session session, byte[] data, int offset, int count)
        {
            throw new NotImplementedException();
        }

        private bool stopFlag;
        private BlockingCollection<Action> commandQueue = new BlockingCollection<Action>();
    }
}
