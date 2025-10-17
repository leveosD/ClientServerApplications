using System.Collections.Concurrent;

namespace ServerNamespace
{
    public class MessageQueue
    {
        private readonly ConcurrentDictionary<string, ConcurrentQueue<TopicMessage>> _messageQueues =
            new ConcurrentDictionary<string, ConcurrentQueue<TopicMessage>>();

        public void EnqueueMessage(string topic, TopicMessage message)
        {
            _messageQueues.AddOrUpdate(topic, new ConcurrentQueue<TopicMessage>(new[] { message }), (key, queue) =>
            {
                queue.Enqueue(message);
                return queue;
            });
        }

        public List<TopicMessage> DequeueMessages(string topic)
        {
            var dequeuedMessages = new List<TopicMessage>();
            if (_messageQueues.TryGetValue(topic, out var queue))
            {
                int length = queue.Count;
                for (int i = 0; i < length && queue.TryDequeue(out var message); i++)
                {
                    dequeuedMessages.Add(message);
                }
            }

            return dequeuedMessages;
        }
    }
}