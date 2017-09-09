using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class CommandDispatcher : IMessageDispatcher, IDisposable
    {
        public static IMessageDispatcher Instance = new CommandDispatcher();
        private CommandDispatcher()
        {
            stopFlag = false;
        }

        public override void Start()
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

        public override void OnDisconnected(INetSession session)
        {
            throw new NotImplementedException();
        }

        public override void OnMessageReceived(INetSession session, byte[] data)
        {
            throw new NotImplementedException();
        }

        private bool stopFlag;
        private BlockingCollection<Action> commandQueue = new BlockingCollection<Action>();
    }
}
